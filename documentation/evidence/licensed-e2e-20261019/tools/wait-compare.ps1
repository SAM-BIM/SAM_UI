param([int]$ProcId, [string]$EvidenceDir, [int]$TimeoutMinutes = 20)
Add-Type -AssemblyName UIAutomationClient, UIAutomationTypes, System.Drawing
$AE = [System.Windows.Automation.AutomationElement]; $TS = [System.Windows.Automation.TreeScope]; $PC = [System.Windows.Automation.PropertyCondition]
$sw = [Diagnostics.Stopwatch]::StartNew(); $cmp = $null
while (-not $cmp -and $sw.Elapsed.TotalMinutes -lt $TimeoutMinutes) {
    foreach ($w in $AE::RootElement.FindAll($TS::Descendants, (New-Object $PC($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::Window)))) {
        try { if ($w.Current.ProcessId -eq $ProcId -and $w.Current.Name -eq 'Compare Tas / OpenStudio') { $cmp = $w } } catch {}
    }
    if (-not $cmp) { Start-Sleep -Seconds 3 }
}
if (-not $cmp) { 'TIMEOUT'; exit 2 }
Start-Sleep -Seconds 3
$banner = $cmp.FindFirst($TS::Descendants, (New-Object $PC($AE::AutomationIdProperty, 'TextBlock_Banner'))).Current.Name
"Banner: $banner"
Add-Content (Join-Path $EvidenceDir 'driver.log') ('{0:o} Banner: {1}' -f (Get-Date).ToUniversalTime(), $banner)
foreach ($tab in $cmp.FindAll($TS::Descendants, (New-Object $PC($AE::ControlTypeProperty, [System.Windows.Automation.ControlType]::TabItem)))) {
    $n = $tab.Current.Name; $p = $null
    if ($tab.TryGetCurrentPattern([System.Windows.Automation.SelectionItemPattern]::Pattern, [ref]$p)) { $p.Select() }
    Start-Sleep -Seconds 1
    $r = $cmp.Current.BoundingRectangle
    $bmp = New-Object System.Drawing.Bitmap ([int]$r.Width), ([int]$r.Height); $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.CopyFromScreen([int]$r.X, [int]$r.Y, 0, 0, $bmp.Size); $bmp.Save((Join-Path $EvidenceDir ("06-compare-tab-" + ($n -replace '[^A-Za-z]', '') + ".png"))); $g.Dispose(); $bmp.Dispose()
}
'Done'
