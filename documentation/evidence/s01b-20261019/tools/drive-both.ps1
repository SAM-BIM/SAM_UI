param(
    [Parameter(Mandatory)] [string] $ModelPath,
    [Parameter(Mandatory)] [string] $EvidenceDir,
    [string] $EpwPath = $null,          # null = answer Yes (model's embedded weather)
    [int] $TimeoutMinutes = 30,
    [int] $AttachPid = 0,
    [switch] $FromFolder
)
# Drives the dev-build SAM Analytical: Simulate > Run Simulation > Both, then captures the comparison window.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$PC = [System.Windows.Automation.PropertyCondition]
New-Item -ItemType Directory -Force $EvidenceDir | Out-Null
$log = Join-Path $EvidenceDir 'driver.log'
function Log($m) { $l = '{0:o} {1}' -f (Get-Date).ToUniversalTime(), $m; try { Add-Content -Path $log -Value $l -ErrorAction Stop } catch {}; Write-Host $l }

function Find-Window([string]$titleLike, [int]$seconds = 60, [int]$procId = 0) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $seconds) {
        foreach ($w in $AE::RootElement.FindAll($TS::Children, [System.Windows.Automation.Condition]::TrueCondition)) {
            try {
                if ($procId -and $w.Current.ProcessId -ne $procId) { continue }
                if ($w.Current.Name -like $titleLike) { return $w }
                # owned dialogs (WPF MessageBox, file dialogs) can be children of the main window
                foreach ($c in $w.FindAll($TS::Children, (New-Object $PC($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Window)))) {
                    if ($c.Current.Name -like $titleLike) { return $c }
                }
            } catch {}
        }
        Start-Sleep -Milliseconds 500
    }
    return $null
}
function Find-ById($root, [string]$id, [int]$seconds = 30) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $seconds) {
        $e = $root.FindFirst($TS::Descendants, (New-Object $PC($AE::AutomationIdProperty, $id)))
        if ($e) { return $e }
        Start-Sleep -Milliseconds 300
    }
    throw "Element '$id' not found"
}
function Find-ByName($root, [string]$name, [int]$seconds = 30) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $seconds) {
        $e = $root.FindFirst($TS::Descendants, (New-Object $PC($AE::NameProperty, $name)))
        if ($e) { return $e }
        Start-Sleep -Milliseconds 300
    }
    throw "Element named '$name' not found"
}
function Invoke-El($e) {
    $p = $null
    if ($e.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$p)) { $p.Invoke(); return }
    if ($e.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$p)) { $p.Select(); return }
    # Win32 file-dialog buttons can expose no pattern: click the centre of the element.
    $r = $e.Current.BoundingRectangle
    [Win32Mouse]::SetCursorPos([int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2)) | Out-Null
    [Win32Mouse]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 80; [Win32Mouse]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
}
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public static class Win32Mouse {
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr e);
}
'@
function Shot($w, [string]$name) {
    try {
        $r = $w.Current.BoundingRectangle
        $bmp = New-Object System.Drawing.Bitmap ([int]$r.Width), ([int]$r.Height)
        $g = [System.Drawing.Graphics]::FromImage($bmp)
        $g.CopyFromScreen([int]$r.X, [int]$r.Y, 0, 0, $bmp.Size)
        $bmp.Save((Join-Path $EvidenceDir $name), [System.Drawing.Imaging.ImageFormat]::Png)
        $g.Dispose(); $bmp.Dispose()
    } catch { Log "screenshot $name failed: $_" }
}

$exe = if ($env:SAM_APP_EXE) { $env:SAM_APP_EXE } else { 'C:\Users\michal\Documents\GitHub\SAM-BIM\SAM_UI\build\SAM Analytical.exe' }
Log "Launching $exe /Path=$ModelPath"
Log ("SAM_BENCHMARK_TAS=" + $env:SAM_BENCHMARK_TAS + " SAM_BENCHMARK_OPENSTUDIO=" + $env:SAM_BENCHMARK_OPENSTUDIO + " SAM_COMMIT=" + $env:SAM_COMMIT)
if ($AttachPid) { $proc = Get-Process -Id $AttachPid } else { $proc = Start-Process -FilePath $exe -ArgumentList "`"/Path=$ModelPath`"" -PassThru }
Log "pid $($proc.Id)"
$main = Find-Window 'SAM Analytical `[*' 240 $proc.Id
if (-not $main) { throw 'Main window did not appear' }
Start-Sleep -Seconds 5   # let the model load
Log "Main window: $($main.Current.Name)"
Shot $main '01-main.png'

if (-not $FromFolder) {
Invoke-El (Find-ById $main 'RibbonTab_Simulate')
Start-Sleep -Seconds 1
Invoke-El (Find-ById $main 'RibbonButton_RunSimulation')
$dlg = Find-Window 'Run Simulation' 30 $proc.Id
if (-not $dlg) { throw 'Run Simulation dialog not found' }
Invoke-El (Find-ById $dlg 'RadioButton_Both')
Start-Sleep -Milliseconds 500
Shot $dlg '02-run-simulation-both.png'
Log 'Run Simulation > Both > Run'
Invoke-El (Find-ById $dlg 'Button_Run')
}

$folder = Find-Window 'Select the folder for the Tas + OpenStudio run*' 60 $proc.Id
if (-not $folder) { throw 'Folder dialog not found' }
Shot $folder '03-folder.png'
Log 'Folder dialog: accepting preset folder (the model folder)'
Invoke-El (Find-ByName $folder 'Select Folder')

$msg = Find-Window 'Run Simulation - Tas + OpenStudio' 60 $proc.Id
if ($msg) {
    Shot $msg '04-weather-question.png'
    if ([string]::IsNullOrEmpty($EpwPath)) { Log 'Weather: Yes (model weather)'; Invoke-El (Find-ByName $msg 'Yes') }
    else {
        Log "Weather: No, EPW $EpwPath"; Invoke-El (Find-ByName $msg 'No')
        $open = Find-Window 'Select the EPW weather file for both engines' 60 $proc.Id
        if (-not $open) { throw 'EPW open dialog not found' }
        Shot $open '04b-epw-dialog.png'
        # The file-name box exposes no Edit/Value pattern here: type it (Alt+N focuses File name).
        $ws = New-Object -ComObject WScript.Shell
        $null = $ws.AppActivate('Select the EPW weather file for both engines'); Start-Sleep -Milliseconds 700
        $ws.SendKeys('%n'); Start-Sleep -Milliseconds 300; $ws.SendKeys($EpwPath); Start-Sleep -Milliseconds 500; $ws.SendKeys('{ENTER}')
        Log "EPW typed: $EpwPath"
    }
}
Log 'Run started; waiting for Compare window'
$cmp = Find-Window 'Compare Tas / OpenStudio' ($TimeoutMinutes * 60) $proc.Id
if (-not $cmp) { Log 'Compare window did not appear before timeout'; exit 2 }
Start-Sleep -Seconds 3
$banner = (Find-ById $cmp 'TextBlock_Banner').Current.Name
Log "Banner: $banner"
Shot $cmp '05-compare-model.png'
foreach ($tab in $cmp.FindAll($TS::Descendants, (New-Object $PC($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::TabItem)))) {
    $n = $tab.Current.Name
    try { Invoke-El $tab; Start-Sleep -Seconds 1; Shot $cmp ("06-compare-tab-" + ($n -replace '[^A-Za-z]', '') + ".png") } catch { Log "tab $n : $_" }
}
# S01b additions: keep the Diagnostics text the window shows, then close the app without saving.
foreach ($tab in $cmp.FindAll($TS::Descendants, (New-Object $PC($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::TabItem)))) {
    if ($tab.Current.Name -like '*Diagnostics*') { try { Invoke-El $tab; Start-Sleep -Seconds 1 } catch {} }
}
$diag = Find-ById $cmp 'TextBox_Diagnostics'
$vp = $null
$diagText = if ($diag.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$vp)) { $vp.Current.Value } else { $diag.Current.Name }
Set-Content -Path (Join-Path $EvidenceDir 'diagnostics.txt') -Value ("Banner: " + $banner + "`r`n`r`n" + $diagText) -Encoding UTF8
Log ("Diagnostics: " + ($diagText -replace "`r`n", ' | ' -replace "`n", ' | '))
foreach ($w in @($cmp, (Find-Window 'SAM Analytical `[*' 10 $proc.Id))) {
    $wp = $null
    if ($w -and $w.TryGetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern, [ref]$wp)) { $wp.Close(); Start-Sleep -Seconds 2 }
}
if (-not $proc.WaitForExit(60000)) { Log 'App did not exit within 60 s' } else { Log "App exited ($($proc.ExitCode))" }
Log 'Done'
