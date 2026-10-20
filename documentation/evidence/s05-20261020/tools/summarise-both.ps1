param([Parameter(Mandatory)] [string[]] $Cases, [string] $Out = 'C:\TasOut\s05\both-summary.txt')
# S05 evidence summary of Run Simulation > Both runs: the manifest's status, snapshot, weather, producer outcomes and what
# each producer reported in its B1a document, plus a line-ending-normalised hash of comparison.csv.
$ErrorActionPreference = 'Stop'
function NormHash([string]$path) {
    if (-not (Test-Path $path)) { return 'missing' }
    $text = [IO.File]::ReadAllText($path) -replace "`r`n", "`n"
    $sha = [Security.Cryptography.SHA256]::Create()
    return 'sha256:' + (($sha.ComputeHash([Text.Encoding]::UTF8.GetBytes($text)) | ForEach-Object { $_.ToString('x2') }) -join '').Substring(0, 16)
}
$lines = @()
foreach ($case in $Cases) {
    $root = "C:\TasOut\s05\runs\$case"
    $runs = @(Get-ChildItem $root -Directory | Where-Object { $_.Name -like '*_TasOpenStudio_*' })
    foreach ($run in $runs) {
        $j = Get-Content (Join-Path $run.FullName 'engine-comparison-run.json') -Raw | ConvertFrom-Json
        $p = $j.provenance
        $lines += "== $case / $($run.Name)"
        $lines += "  status: $($j.status)   started $($j.startedUtc) finished $($j.finishedUtc)"
        $lines += "  snapshot: $($j.snapshot.sha256)"
        $lines += "  weather: $($j.weather.source) $($j.weather.sha256)"
        $lines += "  environment SAM_COMMIT: $($p.environment.SAM_COMMIT)  RUNNER_COMMIT: $($p.environment.RUNNER_COMMIT)"
        foreach ($pr in $j.producers) {
            $lines += ("  producer {0}: succeeded={1} exit={2} {3:0.0}s error={4}" -f $pr.engine, $pr.succeeded, $pr.exitCode, $pr.durationSeconds, $pr.error)
            $r = $p.producers.($pr.engine).reported
            if ($r) {
                $lines += ("    reported: state={0} samCommit={1} runnerCommit={2} engine={3} {4} route={5} weather={6} designDays={7}" -f $r.state, $r.samCommit, $r.runnerCommit, $r.engine.name, $r.engine.version, $r.route, $r.weather.hash, $r.designDaySource)
                $lines += ("    sourceFileHash={0} canonicalModelHash={1} canonicalization={2}" -f $r.sourceFileHash, $r.canonicalModelHash, $r.canonicalizationVersion)
            }
            $lines += ("    executable sha256: {0}" -f $p.producers.($pr.engine).executable.sha256)
        }
        $lines += "  comparison.csv (LF-normalised): $(NormHash (Join-Path $run.FullName 'comparison.csv'))"
        $lines += "  comparison.md  (LF-normalised): $(NormHash (Join-Path $run.FullName 'comparison.md'))"
        if ($p.unknown) { foreach ($k in $p.unknown.PSObject.Properties) { $lines += "  unknown: $($k.Name): $($k.Value)" } }
    }
}
$lines | Set-Content -Path $Out -Encoding UTF8
$lines
