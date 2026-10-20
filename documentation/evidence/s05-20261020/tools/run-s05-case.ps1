param(
    [Parameter(Mandatory)] [string] $Name,
    [Parameter(Mandatory)] [ValidateSet('TasRunSimulation', 'TasEnergySimulationRefusal', 'OpenStudio', 'Both', 'CancelBoth', 'CancelOpenStudio', 'Concurrent')] [string] $Kind,
    [string] $Model = 'C:\TasOut\s05\inputs\box-aperture.json',
    [string] $EpwPath = '',
    [int] $TimeoutMinutes = 20
)
# One S05 licensed acceptance case (test harness): a fresh folder with a copy of the model, an isolated APPDATA/USERPROFILE,
# the freshly built app and producers, then the driver. The S01b drivers are used unchanged for Tas and Both; drive-s05.ps1
# covers OpenStudio, cancellation and concurrent starts.
$ErrorActionPreference = 'Stop'
$S05 = 'C:\TasOut\s05'
$R = Join-Path $S05 "runs\$Name"
if (Test-Path $R) { throw "$R exists; each case uses a fresh folder" }
New-Item -ItemType Directory -Force $R | Out-Null
$modelCopy = Join-Path $R (Split-Path -Leaf $Model)
Copy-Item $Model $modelCopy
$env:APPDATA = "$S05\sandbox\AppData"
$env:USERPROFILE = "$S05\sandbox\Profile"
$env:SAM_APP_EXE = "$S05\app\SAM Analytical.exe"
$env:SAM_BENCHMARK_TAS = (Get-Content "$S05\producers.txt" | Select-String '^tas=').ToString().Substring(4)
$env:SAM_BENCHMARK_OPENSTUDIO = (Get-Content "$S05\producers.txt" | Select-String '^openstudio=').ToString().Substring(11)
$env:PATH = 'C:\Users\michal\Documents\GitHub\SAM-BIM\.icon-migration\runtime\OpenStudio-3.10.0+86d7e215a1-Windows\bin;' + $env:PATH
# Owner decision 2026-10-20: SAM_COMMIT is a temporary requirement of developer-built producers in Both runs, until S06.
# It is set only for the Both routes, to the SAM commit the producers were verified to be built from (producers.txt).
Remove-Item Env:SAM_COMMIT -ErrorAction SilentlyContinue
if ($Kind -in 'Both', 'CancelBoth', 'Concurrent') { $env:SAM_COMMIT = (Get-Content "$S05\producers.txt" | Select-String '^samCommit=').ToString().Substring(10) }
$ev = Join-Path $R 'evidence'
$tools = "$S05\tools"
Start-Transcript -Path (Join-Path $R 'driver-console.txt') | Out-Null
try {
    switch ($Kind) {
        'TasRunSimulation' { & "$tools\drive-tas.ps1" -ModelPath $modelCopy -EvidenceDir $ev -Entry RunSimulationTas -OutputDirectory "$R\out" -ProjectName box -TimeoutMinutes $TimeoutMinutes }
        'TasEnergySimulationRefusal' { & "$tools\drive-tas.ps1" -ModelPath $modelCopy -EvidenceDir $ev -Entry EnergySimulation -OutputDirectory "$R\out" -ProjectName box -BlockTsd -TimeoutMinutes $TimeoutMinutes }
        'Both' { & "$tools\drive-both.ps1" -ModelPath $modelCopy -EvidenceDir $ev -EpwPath $EpwPath -TimeoutMinutes $TimeoutMinutes }
        default { & "$tools\drive-s05.ps1" -Scenario $Kind -ModelPath $modelCopy -EvidenceDir $ev -EpwPath $EpwPath -TimeoutMinutes $TimeoutMinutes }
    }
} finally {
    Stop-Transcript | Out-Null
}
