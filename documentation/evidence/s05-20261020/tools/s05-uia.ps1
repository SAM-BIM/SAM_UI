# S05 licensed acceptance: UI Automation helpers shared by drive-s05.ps1. Dot-source it after setting $EvidenceDir.
# Taken from the S01b drivers (documentation/evidence/s01b-20261019/tools/drive-tas.ps1 and drive-both.ps1) unchanged in
# behaviour; only gathered in one file so the new S05 scenarios use exactly the same window/dialog handling.
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Windows.Forms, System.Drawing
$AE = [System.Windows.Automation.AutomationElement]
$TS = [System.Windows.Automation.TreeScope]
$PC = [System.Windows.Automation.PropertyCondition]
$CT = [System.Windows.Automation.ControlType]
New-Item -ItemType Directory -Force $EvidenceDir | Out-Null
$log = Join-Path $EvidenceDir 'driver.log'
function Log($m) { $l = '{0:o} {1}' -f (Get-Date).ToUniversalTime(), $m; try { Add-Content -Path $log -Value $l -Encoding UTF8 -ErrorAction Stop } catch {}; Write-Host $l }

function Windows-Of([int]$procId) {
    $result = @()
    foreach ($w in $AE::RootElement.FindAll($TS::Children, [System.Windows.Automation.Condition]::TrueCondition)) {
        try {
            if ($w.Current.ProcessId -ne $procId) { continue }
            $result += $w
            foreach ($c in $w.FindAll($TS::Children, (New-Object $PC($AE::ControlTypeProperty, $CT::Window)))) { $result += $c; foreach ($g in $c.FindAll($TS::Children, (New-Object $PC($AE::ControlTypeProperty, $CT::Window)))) { $result += $g } }
        } catch {}
    }
    return $result
}
function Find-Window([string]$titleLike, [int]$seconds, [int]$procId) {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $seconds) {
        foreach ($w in (Windows-Of $procId)) { try { if ($w.Current.Name -like $titleLike) { return $w } } catch {} }
        Start-Sleep -Milliseconds 300
    }
    return $null
}
# A Win32 dialog (#32770): MessageBox (Windows Forms or WPF) and the common file/folder dialogs.
function Find-Dialog([int]$seconds, [int]$procId, [string]$titleLike = '*') {
    $sw = [Diagnostics.Stopwatch]::StartNew()
    while ($sw.Elapsed.TotalSeconds -lt $seconds) {
        foreach ($w in (Windows-Of $procId)) { try { if ($w.Current.ClassName -eq '#32770' -and $w.Current.Name -like $titleLike) { return $w } } catch {} }
        Start-Sleep -Milliseconds 300
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
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public static class S05W32 {
  [DllImport("user32.dll")] public static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, IntPtr l);
  [DllImport("user32.dll")] public static extern bool SetCursorPos(int x, int y);
  [DllImport("user32.dll")] public static extern void mouse_event(uint f, uint x, uint y, uint d, UIntPtr e);
}
'@
Add-Type -TypeDefinition @'
using System; using System.Runtime.InteropServices;
public static class S05W32T { [DllImport("user32.dll", CharSet = CharSet.Unicode)] public static extern IntPtr SendMessage(IntPtr h, uint m, IntPtr w, string l); }
'@
function Invoke-El($e) {
    $p = $null
    if ($e.TryGetCurrentPattern([System.Windows.Automation.InvokePattern]::Pattern, [ref]$p)) { $p.Invoke(); return }
    if ($e.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$p)) { $p.Select(); return }
    # A Win32 button with no pattern (the folder dialog's "Select Folder"): BM_CLICK on its handle, which does not depend
    # on focus or on what is under the mouse (the S05 first OpenStudio attempt's mouse click was lost).
    if ($e.Current.ClassName -eq 'Button' -and $e.Current.NativeWindowHandle -ne 0) {
        [S05W32]::SendMessage([IntPtr]$e.Current.NativeWindowHandle, 0xF5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null; return
    }
    # Win32 file-dialog buttons can expose no pattern: click the centre of the element (as drive-both.ps1).
    $r = $e.Current.BoundingRectangle
    [S05W32]::SetCursorPos([int]($r.X + $r.Width / 2), [int]($r.Y + $r.Height / 2)) | Out-Null
    [S05W32]::mouse_event(2, 0, 0, 0, [UIntPtr]::Zero); Start-Sleep -Milliseconds 80; [S05W32]::mouse_event(4, 0, 0, 0, [UIntPtr]::Zero)
}
# MessageBox Static text and Buttons are raw panes with no patterns: read them through the raw view, click with BM_CLICK.
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
    # BM_CLICK can be ignored by an inactive dialog (here: when TAS has put Notepad in the foreground); WM_ACTIVATE first,
    # as the Win32 documentation of BM_CLICK advises.
    [S05W32]::SendMessage([IntPtr]$dialog.Current.NativeWindowHandle, 0x0006, [IntPtr]1, [IntPtr]::Zero) | Out-Null
    [S05W32]::SendMessage($parts.Buttons[$name], 0xF5, [IntPtr]::Zero, [IntPtr]::Zero) | Out-Null
}
# Records a message box (title, text, buttons) to the log and to <file>, takes its screenshot, and returns its text.
function Capture-Dialog($dialog, [string]$file, [string]$shot) {
    $parts = Dialog-Parts $dialog
    $text = ($parts.Texts -join "`r`n")
    Log ("  dialog '{0}': {1}" -f $dialog.Current.Name, ($text -replace "`r`n", ' | ' -replace "`n", ' | '))
    Log ("  buttons: " + ($parts.Buttons.Keys -join ', '))
    Add-Content -Path (Join-Path $EvidenceDir $file) -Value ("Title: " + $dialog.Current.Name + "`r`n" + $text + "`r`n") -Encoding UTF8
    Shot $dialog $shot
    return $text
}
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
# Engine processes (name, pid, parent) at this moment, for the cancellation and concurrency evidence.
function Engine-Processes([string]$label) {
    $names = 'benchmark-tas.exe', 'benchmark-openstudio.exe', 'openstudio.exe', 'energyplus.exe', 'TBD.exe', 'TasEngine.exe', 'TSD.exe'
    $ps = @(Get-CimInstance Win32_Process | Where-Object { $names -contains $_.Name })
    $line = if ($ps.Count) { ($ps | ForEach-Object { '{0}#{1}(parent {2})' -f $_.Name, $_.ProcessId, $_.ParentProcessId }) -join ', ' } else { 'none' }
    Log ("processes [{0}]: {1}" -f $label, $line)
    Add-Content -Path (Join-Path $EvidenceDir 'processes.txt') -Value ('{0:o} [{1}] {2}' -f (Get-Date).ToUniversalTime(), $label, $line) -Encoding UTF8
    return $ps
}
