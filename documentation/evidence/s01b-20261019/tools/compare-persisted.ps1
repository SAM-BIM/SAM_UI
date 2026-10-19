param(
    [Parameter(Mandatory)] [string] $Baseline,   # persisted model json (WorkflowCalculator <tbd>.json)
    [Parameter(Mandatory)] [string] $Candidate,
    [Parameter(Mandatory)] [string] $Out
)
# Port of the SAM_UI#237 audit-tas.py criterion: exact equality of every numeric leaf of persisted
# simulation-result parameters, aligned by result type / name / load type (+ surface reference).
# Run-generated identities (Guid, Reference, DateTime, Zone Guid, ZoneSurfaceReference) are metadata.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Web.Extensions
$ser = New-Object System.Web.Script.Serialization.JavaScriptSerializer
$ser.MaxJsonLength = [int]::MaxValue
$ser.RecursionLimit = 1000

function Add-Numeric($value, [string]$path, $out) {
    if ($null -eq $value) { return }
    if ($value -is [bool]) { return }
    if ($value -is [int] -or $value -is [long] -or $value -is [double] -or $value -is [decimal]) { $out[$path] = [double]$value; return }
    if ($value -is [System.Collections.IDictionary]) {
        foreach ($k in $value.Keys) { if ($k -notin @('Guid','Reference','DateTime')) { Add-Numeric $value[$k] ($path + '/' + $k) $out } }
        return
    }
    if ($value -is [System.Collections.IEnumerable] -and -not ($value -is [string])) {
        $i = 0; foreach ($v in $value) { Add-Numeric $v ($path + '/' + $i) $out; $i++ }
    }
}

function Extract([string]$file) {
    $j = $ser.DeserializeObject([IO.File]::ReadAllText($file))
    if ($j -is [object[]] -and $j.Count -eq 1) { $j = $j[0] }
    $out = @{}; $counts = @{}; $seen = @{}
    foreach ($group in $j['AdjacencyCluster']['Objects']) {
        $key0 = [string]$group['Key']
        if (-not $key0.EndsWith('SimulationResult')) { continue }
        $counts[$key0] = @($group['Value']).Count
        foreach ($entry in $group['Value']) {
            $obj = $entry['Value']; $params = @{}
            foreach ($ps in @($obj['ParameterSets'])) {
                if ($null -eq $ps) { continue }
                foreach ($p in @($ps['Parameters'])) {
                    if ($null -eq $p) { continue }
                    $n = $p['Name']
                    if ($n -and $n -notin @('Zone Guid','ZoneSurfaceReference')) { $params[$n] = $p['Value'] }
                }
            }
            $key = $key0 + '/' + $obj['Name'] + '/' + [string]$params['Load Type']
            if ($key0.EndsWith('SurfaceSimulationResult')) { $key += '/' + [string]$obj['Reference'] }
            if ($seen.ContainsKey($key)) { throw "duplicate result identity $key" }
            $seen[$key] = 1
            Add-Numeric $params $key $out
        }
    }
    return @{ numbers = $out; counts = $counts }
}

$a = Extract $Baseline; $b = Extract $Candidate
$keys = @($a.numbers.Keys) + @($b.numbers.Keys) | Sort-Object -Unique
$diffs = New-Object System.Collections.ArrayList
foreach ($k in $keys) {
    $va = $a.numbers[$k]; $vb = $b.numbers[$k]
    if (-not ($a.numbers.ContainsKey($k) -and $b.numbers.ContainsKey($k) -and $va -eq $vb)) {
        [void]$diffs.Add([ordered]@{ path = $k; baseline = $va; candidate = $vb })
    }
}
$report = [ordered]@{
    criterion = 'Exact equality of every numeric leaf of persisted simulation parameters, aligned by result type/name/load type and surface reference. No tolerance. Run-generated identities and timestamps are metadata.'
    baseline = $Baseline; candidate = $Candidate
    baselineCounts = $a.counts; candidateCounts = $b.counts
    baselineNumbers = $a.numbers.Count; candidateNumbers = $b.numbers.Count
    exactMatch = ($diffs.Count -eq 0)
    differences = $diffs
}
$json = $report | ConvertTo-Json -Depth 6
[IO.File]::WriteAllText($Out, $json)
"baselineNumbers=$($a.numbers.Count) candidateNumbers=$($b.numbers.Count) differences=$($diffs.Count) exactMatch=$($diffs.Count -eq 0)"
$a.counts.GetEnumerator() | ForEach-Object { "  $($_.Key) = $($_.Value) / $($b.counts[$_.Key])" }
