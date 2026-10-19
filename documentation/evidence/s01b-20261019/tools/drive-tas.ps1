param(
    [Parameter(Mandatory)] [string] $ModelPath,
    [Parameter(Mandatory)] [string] $EvidenceDir,
    [Parameter(Mandatory)] [ValidateSet('EnergySimulation', 'RunSimulationTas')] [string] $Entry,
    [Parameter(Mandatory)] [string] $OutputDirectory,
    [string] $ProjectName = 'box',
    [switch] $BlockTsd,                 # S01b refusal injection: a DIRECTORY at <OutputDirectory>\<ProjectName>.tsd, so TAS cannot write it
    [int] $TimeoutMinutes = 30
)
# S01b: drives the dev-build SAM Analytical through Energy Simulation or Run Simulation > Tas, then captures the closing
# message box and the main window. Modelled on documentation/evidence/licensed-e2e-20261019/tools/drive-both.ps1 (#239).
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$PC = [System.Windows.Automation.PropertyCondition]
$CT = [System.Windows.Automation.ControlType]
New-Item -ItemType Directory -Force $EvidenceDir | Out-Null
New-Item -ItemType Directory -Force $OutputDirectory | Out-Null
$log = Join-Path $EvidenceDir 'driver.log'
function Log($m) { $l = '{0:o} {1}' -f (Get-Date).ToUniversalTime(), $m; try { Add-Content -Path $log -Value $l -Encoding UTF8 -ErrorAction Stop } catch {}; Write-Host $l }

function Windows-Of([int]$procId) {
    $result = @()
    foreach ($w in $AE::RootElement.FindAll($TS::Children, [System.Windows.Automation.Condition]::TrueCondition)) {
        try {
            if ($w.Current.ProcessId -ne $procId) { continue }
            $result += $w
            foreach ($c in $w.FindAll($TS::Children, (New-Object $PC($AE::ControlTypeProperty, $CT::Window)))) { $result += $c }
        } catch {}
    }
    return $result
}
function Find-Window([string]$titleLike, [int]$seconds, [int]$procId) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $seconds) {
        foreach ($w in (Windows-Of $procId)) { try { if ($w.Current.Name -like $titleLike) { return $w } } catch {} }
        Start-Sleep -Milliseconds 500
    }
    return $null
}
# A Win32 dialog (#32770): the Windows Forms MessageBox Energy Simulation closes with.
function Find-Dialog([int]$seconds, [int]$procId) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $seconds) {
        foreach ($w in (Windows-Of $procId)) { try { if ($w.Current.ClassName -eq '#32770') { return $w } } catch {} }
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
    throw "Element '$($e.Current.Name)' cannot be invoked"
}
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public static class W32 { [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l); }
'@
# The Windows Forms MessageBox exposes its Static text and Buttons to UI Automation only as raw panes with no patterns:
# read them through the raw view and click with BM_CLICK.
function Dialog-Parts($dialog) {
    $walker = [System.Windows.Automation.TreeWalker]::RawViewWalker
    $texts = @(); $buttons = @{}
    $c = $walker.GetFirstChild($dialog)
    while ($c) {
        if ($c.Current.ClassName -eq 'Static' -and $c.Current.Name) { $texts += $c.Current.Name }
        if ($c.Current.ClassName -eq 'Button') { $buttons[($c.Current.Name -replace '&', '')] = [IntPtr]$c.Current.NativeWindowHandle }
        $c = $walker.GetNextSibling($c)
    }
    return @{ Texts = $texts; Buttons = $buttons }
}
function Click-DialogButton($dialog, [string]$name) {
    $parts = Dialog-Parts $dialog
    if (-not $parts.Buttons.ContainsKey($name)) { throw "No '$name' button; buttons: $($parts.Buttons.Keys -join ', ')" }
    [W32]::SendMessage($parts.Buttons[$name], 0xF5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
}
function Value-Of($e) { $p = $null; if ($e.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$p)) { return $p.Current.Value }; return $e.Current.Name }
function Set-Value($e, [string]$v) { $p = $null; if (-not $e.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$p)) { throw "no ValuePattern on $($e.Current.AutomationId)" }; $p.SetValue($v) }
function Toggle-Of($e) { $p = $null; if ($e.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$p)) { return $p.Current.ToggleState }; return '?' }
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

$exe = if ($env:SAM_APP_EXE) { $env:SAM_APP_EXE } else { throw 'Set SAM_APP_EXE to the SAM Analytical.exe under test' }
Log "Entry $Entry; app $exe; model $ModelPath; output $OutputDirectory\$ProjectName; BlockTsd=$BlockTsd"
$proc = Start-Process -FilePath $exe -ArgumentList "`"/Path=$ModelPath`"" -PassThru
Log "pid $($proc.Id)"
$main = Find-Window 'SAM Analytical `[*' 240 $proc.Id
if (-not $main) { throw 'Main window did not appear' }
Start-Sleep -Seconds 5
$title_Before = $main.Current.Name
Log "Main window: $title_Before"

Invoke-El (Find-ById $main 'RibbonTab_Simulate')
Start-Sleep -Seconds 1
if ($Entry -eq 'EnergySimulation') {
    Invoke-El (Find-ById $main 'RibbonButton_EnergySimulation')
} else {
    Invoke-El (Find-ById $main 'RibbonButton_RunSimulation')
    $dlg = Find-Window 'Run Simulation' 30 $proc.Id
    if (-not $dlg) { throw 'Run Simulation dialog not found' }
    Invoke-El (Find-ById $dlg 'RadioButton_Tas')
    Start-Sleep -Milliseconds 500
    Shot $dlg '02-run-simulation-tas.png'
    Log 'Run Simulation > Tas > Run'
    Invoke-El (Find-ById $dlg 'Button_Run')
}

$sim = Find-Window 'Convert to TAS' 60 $proc.Id
if (-not $sim) { throw 'Convert to TAS (Simulate) dialog not found' }
Start-Sleep -Seconds 1
Set-Value (Find-ById $sim 'textBox_ProjectName') $ProjectName
Set-Value (Find-ById $sim 'textBox_OutputDirectory') $OutputDirectory
# A real annual simulation: without Full Year Simulation the workflow sizes only and never calls TAS simulate.
$fullYear = Find-ById $sim 'checkBox_FullYearSimulation'
$tp = $null
if ($fullYear.TryGetCurrentPattern([System.Windows.Automation.TogglePattern]::Pattern, [ref]$tp) -and $tp.Current.ToggleState -ne 'On') { $tp.Toggle(); Log 'Full Year Simulation ticked' }
Start-Sleep -Milliseconds 500
foreach ($id in 'checkBox_Simulate', 'checkBox_FullYearSimulation', 'checkBox_Sizing', 'checkBox_UnmetHours', 'checkBox_CreateTPD', 'checkBox_CreateSAP', 'checkBox_CreatePartL', 'checkBox_RoomDataSheets', 'checkBox_UseWidths', 'checkBox_UpdateConstructionLayersByPanelType') {
    try { Log ("  {0} = {1}" -f $id, (Toggle-Of (Find-ById $sim $id 5))) } catch { Log "  $id : $_" }
}
foreach ($id in 'textBox_ProjectName', 'textBox_OutputDirectory', 'textBox_FullYearSimulation_From', 'textBox_FullYearSimulation_To') {
    try { Log ("  {0} = {1}" -f $id, (Value-Of (Find-ById $sim $id 5))) } catch { Log "  $id : $_" }
}
Shot $sim '03-simulate-dialog.png'

$path_TSD = Join-Path $OutputDirectory ($ProjectName + '.tsd')
if ($BlockTsd) {
    New-Item -ItemType Directory -Force $path_TSD | Out-Null
    Log "Refusal injected: directory at $path_TSD"
}

$started = Get-Date
Log 'OK'
Invoke-El (Find-ById $sim 'button_OK')

$box = Find-Dialog ($TimeoutMinutes * 60) $proc.Id
if (-not $box) { Log 'No closing message box before the timeout'; exit 2 }
Start-Sleep -Seconds 1
Log ("Message box after {0:0} s, title: '{1}'" -f ((Get-Date) - $started).TotalSeconds, $box.Current.Name)
$parts = Dialog-Parts $box
foreach ($t in $parts.Texts) {
    Log ("  text: " + ($t -replace "`r`n", ' | ' -replace "`n", ' | '))
    Set-Content -Path (Join-Path $EvidenceDir 'message-box.txt') -Value ("Title: " + $box.Current.Name + "`r`n" + $t) -Encoding UTF8
}
Log ("  buttons: " + ($parts.Buttons.Keys -join ', '))
Shot $box '04-message-box.png'
Click-DialogButton $box 'OK'
Start-Sleep -Seconds 2

$main = Find-Window 'SAM Analytical `[*' 30 $proc.Id
Log "Main window after: $($main.Current.Name) (before: $title_Before)"
Shot $main '05-main-after.png'

Get-ChildItem -Force $OutputDirectory | ForEach-Object { Log ("  output: {0} {1} {2:o}" -f $_.Name, $(if ($_.PSIsContainer) { '<dir>' } else { $_.Length }), $_.LastWriteTimeUtc) }

# Close without saving: the run must not write the model file.
$wp = $null
if ($main.TryGetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern, [ref]$wp)) { $wp.Close() }
$save = Find-Dialog 20 $proc.Id
if ($save) {
    Log ("Close prompt: '{0}' {1}" -f $save.Current.Name, ((Dialog-Parts $save).Texts -join ' | '))
    try { Click-DialogButton $save 'No' } catch { Log "$_" }
}
if (-not $proc.WaitForExit(60000)) { Log 'App did not exit within 60 s' } else { Log "App exited ($($proc.ExitCode))" }
Log 'Done'
