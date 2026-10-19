param([Parameter(Mandatory)][string]$Name, [Parameter(Mandatory)][ValidateSet('base','ab')][string]$App, [switch]$RefuseTas)
# One S01b licensed Both case: fresh folder with a copy of the box model (the run folder is created beside it), the explicit
# Budapest EPW, real benchmark-openstudio; benchmark-tas is the real one, or the blocking wrapper that runs it (-RefuseTas).
$R = "C:\TasOut\s01b\runs\$Name"
New-Item -ItemType Directory -Force $R | Out-Null
Copy-Item C:\TasOut\s01b\inputs\box-aperture.json "$R\box-aperture.json"
$realTas = 'C:\Users\michal\Documents\GitHub\SAM-BIM\SAM_Tas\build\benchmark-tas.exe'
$env:S01B_REAL_TAS = $realTas
$env:SAM_BENCHMARK_TAS = if ($RefuseTas) { 'C:\TasOut\s01b\wrapper\out\benchmark-tas.exe' } else { $realTas }
$env:SAM_BENCHMARK_OPENSTUDIO = 'C:\Users\michal\Documents\GitHub\SAM-BIM\SAM_OpenStudio\benchmark\SAM.Analytical.OpenStudio.Benchmark\bin\x64\Release\net8.0\benchmark-openstudio.exe'
$env:SAM_COMMIT = '62b5415697d1a5af143c6b581b7d42fbd6551ca8'
$env:PATH = 'C:\Users\michal\Documents\GitHub\SAM-BIM\.icon-migration\runtime\OpenStudio-3.10.0+86d7e215a1-Windows\bin;' + $env:PATH
$env:SAM_APP_EXE = "C:\TasOut\s01b\app-$App\SAM Analytical.exe"
& C:\TasOut\s01b\tools\drive-both.ps1 -ModelPath "$R\box-aperture.json" -EvidenceDir "$R\evidence" -EpwPath 'C:\TasOut\s01b\inputs\Budapest.epw' -TimeoutMinutes 20
