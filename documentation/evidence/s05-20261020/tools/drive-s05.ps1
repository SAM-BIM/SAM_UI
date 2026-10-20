param(
    [Parameter(Mandatory)] [ValidateSet('OpenStudio', 'CancelOpenStudio', 'CancelBoth', 'Concurrent')] [string] $Scenario,
    [Parameter(Mandatory)] [string] $ModelPath,
    [Parameter(Mandatory)] [string] $EvidenceDir,
    [string] $EpwPath = $null,          # null/empty = answer Yes (the model's embedded weather)
    [int] $TimeoutMinutes = 20
)
# S05 licensed acceptance driver (test harness, not product code) for the cases the S01b drivers do not cover:
#   OpenStudio        case 2: Run Simulation > OpenStudio from the ribbon, to the closing summary.
#   CancelOpenStudio  case 8: Run Simulation > OpenStudio, Cancel in the progress window while EnergyPlus runs.
#   CancelBoth        case 8: Run Simulation > Both, Cancel in the progress window while the Tas producer runs.
#   Concurrent        case 9: while Run Simulation > Both runs, start Energy Simulation, Run Simulation and OpenStudio
#                     Simulation from the ribbon; then, with the comparison window open, while an OpenStudio Simulation
#                     runs, start the comparison window's Run. Each must be refused with the S02 message.
# The app under test is $env:SAM_APP_EXE; the run folder is the model's folder (each case uses its own fresh folder).
$ErrorActionPreference = 'Stop'
. (Join-Path $PSScriptRoot 's05-uia.ps1')

$exe = if ($env:SAM_APP_EXE) { $env:SAM_APP_EXE } else { throw 'Set SAM_APP_EXE to the SAM Analytical.exe under test' }
$folder_Model = Split-Path -Parent $ModelPath
Log "Scenario $Scenario; app $exe; model $ModelPath; EPW '$EpwPath'"
Log ("SAM_BENCHMARK_TAS=" + $env:SAM_BENCHMARK_TAS + " SAM_BENCHMARK_OPENSTUDIO=" + $env:SAM_BENCHMARK_OPENSTUDIO + " SAM_COMMIT=" + $env:SAM_COMMIT)
Get-ChildItem -Force $folder_Model | ForEach-Object { Log ("  before: {0}" -f $_.Name) }
$proc = Start-Process -FilePath $exe -ArgumentList "`"/Path=$ModelPath`"" -PassThru
Log "pid $($proc.Id)"
$main = Find-Window 'SAM Analytical `[*' 240 $proc.Id
if (-not $main) { throw 'Main window did not appear' }
Start-Sleep -Seconds 5
$title_Before = $main.Current.Name
Log "Main window: $title_Before"
Shot $main '01-main.png'

function Find-Progress([string]$title, [int]$seconds) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $seconds) {
        foreach ($w in (Windows-Of $proc.Id)) { try { if ($w.Current.Name -eq $title -and $w.Current.ClassName -ne '#32770') { return $w } } catch {} }
        Start-Sleep -Milliseconds 250
    }
    return $null
}
function Progress-Alive([string]$title) {
    foreach ($w in (Windows-Of $proc.Id)) { try { if ($w.Current.Name -eq $title -and $w.Current.ClassName -ne '#32770') { return $true } } catch {} }
    return $false
}
function Wait-EngineProcess([string]$name, [int]$seconds) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $seconds) {
        $p = @(Get-CimInstance Win32_Process -Filter ("Name='{0}'" -f $name))
        if ($p.Count -gt 0) { return $true }
        Start-Sleep -Milliseconds 250
    }
    return $false
}
function Start-FromRunSimulation([string]$engine) {
    Invoke-El (Find-ById $main 'RibbonTab_Simulate')
    Start-Sleep -Seconds 1
    Invoke-El (Find-ById $main 'RibbonButton_RunSimulation')
    $dlg = Find-Window 'Run Simulation' 30 $proc.Id
    if (-not $dlg) { throw 'Run Simulation dialog not found' }
    Invoke-El (Find-ById $dlg ('RadioButton_' + $engine))
    Start-Sleep -Milliseconds 500
    Shot $dlg ('02-run-simulation-' + $engine.ToLowerInvariant() + '.png')
    Log "Run Simulation > $engine > Run"
    Invoke-El (Find-ById $dlg 'Button_Run')
}
# The folder dialog (preset: the model's folder), then the weather question and, for an explicit EPW, the open dialog.
function Answer-Setup([string]$folderTitle, [string]$weatherTitle, [string]$epwTitle, [string]$prefix) {
    $folder = Find-Window $folderTitle 60 $proc.Id
    if (-not $folder) { throw "Folder dialog '$folderTitle' not found" }
    Shot $folder ($prefix + '-folder.png')
    Log 'Folder dialog: accepting the preset folder (the model folder)'
    Invoke-El (Find-ByName $folder 'Select Folder')
    $msg = Find-Dialog 60 $proc.Id $weatherTitle
    if (-not $msg) { Log 'No weather question (model has no embedded weather)' }
    else {
        Shot $msg ($prefix + '-weather-question.png')
        if ([string]::IsNullOrEmpty($EpwPath)) { Log 'Weather: Yes (model weather)'; Click-DialogButton $msg 'Yes'; return }
        Log "Weather: No, EPW $EpwPath"; Click-DialogButton $msg 'No'
    }
    if ([string]::IsNullOrEmpty($EpwPath)) { return }
    $open = Find-Window $epwTitle 60 $proc.Id
    if (-not $open) { throw 'EPW open dialog not found' }
    Shot $open ($prefix + '-epw-dialog.png')
    # The file-name box (Edit inside ComboBox 1148) exposes no Value pattern: set its text with WM_SETTEXT and press Open
    # (button id 1) with BM_CLICK. Unlike drive-both.ps1's SendKeys this does not depend on keyboard focus.
    Start-Sleep -Milliseconds 700
    $edit = $null
    foreach ($x in $open.FindAll($TS::Descendants, (New-Object $PC($AE::ClassNameProperty, 'Edit')))) { if ($x.Current.AutomationId -eq '1148') { $edit = $x } }
    if (-not $edit) { throw 'EPW file-name box not found' }
    [S05W32T]::SendMessage([IntPtr]$edit.Current.NativeWindowHandle, 0x000C, [IntPtr]::Zero, $EpwPath) | Out-Null
    Start-Sleep -Milliseconds 300
    $ok = $null
    foreach ($x in $open.FindAll($TS::Descendants, (New-Object $PC($AE::ClassNameProperty, 'Button')))) { if ($x.Current.AutomationId -eq '1') { $ok = $x } }
    if (-not $ok) { throw 'EPW Open button not found' }
    [S05W32]::SendMessage([IntPtr]$ok.Current.NativeWindowHandle, 0xF5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
    Log "EPW set: $EpwPath"
}
function Capture-Comparison([string]$prefix) {
    $cmp = Find-Window 'Compare Tas / OpenStudio' ($TimeoutMinutes * 60) $proc.Id
    if (-not $cmp) { Log 'Compare window did not appear before the timeout'; return $null }
    Start-Sleep -Seconds 3
    $banner = (Find-ById $cmp 'TextBlock_Banner').Current.Name
    Log "Banner: $banner"
    Shot $cmp ($prefix + '-compare.png')
    foreach ($tab in $cmp.FindAll($TS::Descendants, (New-Object $PC($AE::ControlTypeProperty, $CT::TabItem)))) {
        if ($tab.Current.Name -like '*Diagnostics*') { try { Invoke-El $tab; Start-Sleep -Seconds 1 } catch {} }
    }
    $diag = Find-ById $cmp 'TextBox_Diagnostics'
    $vp = $null
    $diagText = if ($diag.TryGetCurrentPattern([System.Windows.Automation.ValuePattern]::Pattern, [ref]$vp)) { $vp.Current.Value } else { $diag.Current.Name }
    Set-Content -Path (Join-Path $EvidenceDir ($prefix + '-diagnostics.txt')) -Value ("Banner: " + $banner + "`r`n`r`n" + $diagText) -Encoding UTF8
    Shot $cmp ($prefix + '-compare-diagnostics.png')
    return $cmp
}
# The OpenStudio closing summary: a message box titled "OpenStudio Simulation" (the progress window has the same title
# but is not a #32770 dialog). Answers No to "Open the run folder?" (or OK).
function Capture-OpenStudioSummary([string]$prefix) {
    $box = Find-Dialog ($TimeoutMinutes * 60) $proc.Id 'OpenStudio Simulation'
    if (-not $box) { Log 'No OpenStudio summary before the timeout'; return $null }
    Start-Sleep -Seconds 1
    $text = Capture-Dialog $box ($prefix + '-summary.txt') ($prefix + '-summary.png')
    $parts = Dialog-Parts $box
    if ($parts.Buttons.ContainsKey('No')) { Click-DialogButton $box 'No' } else { Click-DialogButton $box 'OK' }
    Start-Sleep -Seconds 2
    return $text
}
# One concurrent start: invoke a ribbon button (or the comparison window's Run) and expect the S02 refusal box.
function Attempt-Start($root, [string]$id, [string]$label, [string]$progressTitle, [string]$refusalTitle = 'Simulation already running') {
    $alive = Progress-Alive $progressTitle
    Log ("Concurrent attempt: {0} (first run's progress window open: {1})" -f $label, $alive)
    Engine-Processes ("before " + $label) | Out-Null
    Invoke-El (Find-ById $root $id)
    $box = Find-Dialog 20 $proc.Id $refusalTitle
    if ($box) {
        $text = Capture-Dialog $box 'refusals.txt' ('refusal-' + ($label -replace '[^A-Za-z]', '') + '.png')
        Click-DialogButton $box 'OK'
        Start-Sleep -Milliseconds 700
        $result = "REFUSED: $text"
    } else {
        $other = @(Windows-Of $proc.Id | ForEach-Object { try { "'" + $_.Current.Name + "' (" + $_.Current.ClassName + ")" } catch {} })
        $result = "NOT REFUSED; windows now: " + ($other -join ', ')
    }
    $aliveAfter = Progress-Alive $progressTitle
    Log ("  -> {0}; first run still running after the attempt: {1}" -f $result, $aliveAfter)
    Add-Content -Path (Join-Path $EvidenceDir 'concurrency.txt') -Value ("{0}: {1} | first run's progress window open before: {2}, after: {3}" -f $label, $result, $alive, $aliveAfter) -Encoding UTF8
}

$status = 0
switch ($Scenario) {
    'OpenStudio' {
        Start-FromRunSimulation 'OpenStudio'
        Answer-Setup 'Select the folder for the OpenStudio run*' 'OpenStudio Simulation' 'Select the EPW weather file for the OpenStudio run' '03'
        $started = Get-Date
        if (Find-Progress 'OpenStudio Simulation' 60) { Log 'Progress window up' }
        $text = Capture-OpenStudioSummary '04'
        Log ("Summary after {0:0} s" -f ((Get-Date) - $started).TotalSeconds)
        if (-not $text) { $status = 2 }
    }
    'CancelOpenStudio' {
        Start-FromRunSimulation 'OpenStudio'
        Answer-Setup 'Select the folder for the OpenStudio run*' 'OpenStudio Simulation' 'Select the EPW weather file for the OpenStudio run' '03'
        $progress = Find-Progress 'OpenStudio Simulation' 60
        if (-not $progress) { throw 'OpenStudio progress window not found' }
        $ep = Wait-EngineProcess 'energyplus.exe' 180
        if (-not $ep) { Log 'energyplus.exe never appeared; cancelling anyway' }
        Engine-Processes 'running, before Cancel' | Out-Null
        Shot $progress '04-progress-before-cancel.png'
        Log 'Cancel'
        Invoke-El (Find-ById $progress 'Button_Cancel')
        $text = Capture-OpenStudioSummary '05'
        Start-Sleep -Seconds 3
        Engine-Processes '3 s after the summary' | Out-Null
        if (-not $text) { $status = 2 }
    }
    'CancelBoth' {
        Start-FromRunSimulation 'Both'
        Answer-Setup 'Select the folder for the Tas + OpenStudio run*' 'Run Simulation - Tas + OpenStudio' 'Select the EPW weather file for both engines' '03'
        $progress = Find-Progress 'Run Simulation - Tas + OpenStudio' 60
        if (-not $progress) { throw 'Both progress window not found' }
        $tas = Wait-EngineProcess 'benchmark-tas.exe' 180
        if (-not $tas) { Log 'benchmark-tas.exe never appeared; cancelling anyway' }
        Start-Sleep -Seconds 4   # let the producer reach TAS
        Engine-Processes 'running, before Cancel' | Out-Null
        Shot $progress '04-progress-before-cancel.png'
        Log 'Cancel'
        Invoke-El (Find-ById $progress 'Button_Cancel')
        $cmp = Capture-Comparison '05'
        Start-Sleep -Seconds 2
        Engine-Processes 'after the comparison window opened' | Out-Null
        if (-not $cmp) { $status = 2 }
    }
    'Concurrent' {
        Start-FromRunSimulation 'Both'
        Answer-Setup 'Select the folder for the Tas + OpenStudio run*' 'Run Simulation - Tas + OpenStudio' 'Select the EPW weather file for both engines' '03'
        if (-not (Find-Progress 'Run Simulation - Tas + OpenStudio' 60)) { throw 'Both progress window not found' }
        Log 'Both is running; starting the other entry points'
        $main = Find-Window 'SAM Analytical `[*' 10 $proc.Id
        Attempt-Start $main 'RibbonButton_EnergySimulation' 'Energy Simulation (ribbon)' 'Run Simulation - Tas + OpenStudio'
        Attempt-Start $main 'RibbonButton_RunSimulation' 'Run Simulation (ribbon)' 'Run Simulation - Tas + OpenStudio'
        Attempt-Start $main 'RibbonButton_OpenStudioSimulation' 'OpenStudio Simulation (ribbon)' 'Run Simulation - Tas + OpenStudio'
        $cmp = Capture-Comparison '05'
        if (-not $cmp) { $status = 2 }
        else {
            # Phase 2: the comparison window's Run while an OpenStudio Simulation runs for the same model.
            Log 'Phase 2: OpenStudio Simulation from the ribbon, then the comparison window Run while it runs'
            Invoke-El (Find-ById $main 'RibbonButton_OpenStudioSimulation')
            Answer-Setup 'Select the folder for the OpenStudio run*' 'OpenStudio Simulation' 'Select the EPW weather file for the OpenStudio run' '06'
            if (-not (Find-Progress 'OpenStudio Simulation' 60)) { throw 'OpenStudio progress window not found' }
            Attempt-Start $cmp 'Button_Run' 'Comparison window Run' 'OpenStudio Simulation' 'Compare Tas / OpenStudio'
            Attempt-Start $main 'RibbonButton_EnergySimulation' 'Energy Simulation during OpenStudio (ribbon)' 'OpenStudio Simulation'
            $text = Capture-OpenStudioSummary '07'
            if (-not $text) { $status = 2 }
        }
    }
}

$main = Find-Window 'SAM Analytical `[*' 30 $proc.Id
Log "Main window after: $($main.Current.Name) (before: $title_Before)"
Shot $main '09-main-after.png'
Get-ChildItem -Force $folder_Model | ForEach-Object { Log ("  after: {0} {1}" -f $_.Name, $(if ($_.PSIsContainer) { '<dir>' } else { $_.Length })) }

# Close without saving: no route may write the model file.
foreach ($w in @(Windows-Of $proc.Id)) {
    try { if ($w.Current.Name -like 'Compare Tas / OpenStudio*') { $wp = $null; if ($w.TryGetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern, [ref]$wp)) { $wp.Close(); Start-Sleep -Seconds 2 } } } catch {}
}
$wp = $null
if ($main -and $main.TryGetCurrentPattern([System.Windows.Automation.WindowPattern]::Pattern, [ref]$wp)) { $wp.Close() }
$save = Find-Dialog 15 $proc.Id
if ($save) {
    Log ("Close prompt: '{0}' {1}" -f $save.Current.Name, ((Dialog-Parts $save).Texts -join ' | '))
    try { Click-DialogButton $save 'No' } catch { Log "$_" }
}
if (-not $proc.WaitForExit(60000)) { Log 'App did not exit within 60 s' } else { Log "App exited ($($proc.ExitCode))" }
Log "Done (driver status $status)"
exit $status
