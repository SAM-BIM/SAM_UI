param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][ValidateSet('base','ab')][string]$App, [Parameter(Mandatory)][ValidateSet('EnergySimulation','RunSimulationTas')][string]$Entry, [switch]$BlockTsd, [string]$ReuseFrom = $null)
# One S01b licensed Tas case: a fresh run folder with a copy of the box model, then drive-tas.ps1.
# -ReuseFrom: start from a copy of an earlier case's output folder (folder reuse: its box.tbd/box.json/box.tsd stay).
$R = "C:\TasOut\s01b\runs\$Name"
New-Item -ItemType Directory -Force $R | Out-Null
Copy-Item C:\TasOut\s01b\inputs\box-aperture.json "$R\box-aperture.json"
if ($ReuseFrom) { Copy-Item -Recurse "C:\TasOut\s01b\runs\$ReuseFrom\out" "$R\out"; if (Test-Path "$R\out\box.tsd" -PathType Leaf) { Remove-Item "$R\out\box.tsd" } }
$env:SAM_APP_EXE = "C:\TasOut\s01b\app-$App\SAM Analytical.exe"
$a = @{ ModelPath = "$R\box-aperture.json"; EvidenceDir = "$R\evidence"; Entry = $Entry; OutputDirectory = "$R\out"; ProjectName = 'box'; TimeoutMinutes = 20 }
if ($BlockTsd) { $a.BlockTsd = $true }
& C:\TasOut\s01b\tools\drive-tas.ps1 @a
