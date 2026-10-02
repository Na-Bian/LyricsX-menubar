# LyricsX Windows taskbar overlay spike (v2: DPI-aware, physical coordinates)
$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class NativeMethods {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    public static extern IntPtr FindWindow(string cls, string title);
    [DllImport("user32.dll")]
    public static extern bool GetWindowRect(IntPtr hWnd, out RECT rect);
    [DllImport("user32.dll")]
    public static extern int GetWindowLong(IntPtr hWnd, int index);
    [DllImport("user32.dll")]
    public static extern int SetWindowLong(IntPtr hWnd, int index, int value);
    [DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr insertAfter, int x, int y, int cx, int cy, uint flags);
    [StructLayout(LayoutKind.Sequential)]
    public struct RECT { public int Left, Top, Right, Bottom; }
}
'@
[void][NativeMethods]::SetProcessDPIAware()

Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

$log = Join-Path $env:TEMP 'lyricsx-spike.log'
function Log([string]$m) { ("{0} {1}" -f (Get-Date -Format o), $m) | Out-File -Append -FilePath $log }

trap { ('FATAL ' + $_.Exception.Message + ' @ ' + $_.InvocationInfo.PositionMessage) | Out-File -Append -FilePath $log; exit 1 }

Log ('boot session=' + [System.Diagnostics.Process]::GetCurrentProcess().SessionId)

$tray = New-Object NativeMethods+RECT
$trayHwnd = [NativeMethods]::FindWindow('Shell_TrayWnd', $null)
if ($trayHwnd -eq [IntPtr]::Zero) { throw 'Shell_TrayWnd not found' }
[void][NativeMethods]::GetWindowRect($trayHwnd, [ref]$tray)
$tbH = $tray.Bottom - $tray.Top
Log ("Shell_TrayWnd=0x{0:x} rect=({1},{2})-({3},{4}) h={5} physical" -f $trayHwnd.ToInt64(), $tray.Left, $tray.Top, $tray.Right, $tray.Bottom, $tbH)

Log 'step: before-form'
$form = New-Object System.Windows.Forms.Form
$form.FormBorderStyle = 'None'
$form.ShowInTaskbar = $false
$form.TopMost = $true
$form.StartPosition = 'Manual'
$width = [int]($tbH * 7)
$height = $tbH - 12
$form.Bounds = New-Object System.Drawing.Rectangle(($tray.Left + 16), ($tray.Top + 6), $width, $height)
$form.BackColor = [System.Drawing.Color]::Black

$label = New-Object System.Windows.Forms.Label
$label.Text = ' LyricsX spike ♪ 验证任务栏左侧覆盖窗口 任务栏歌词测试行'
$label.ForeColor = [System.Drawing.Color]::White
$label.Font = New-Object System.Drawing.Font('Segoe UI', [float]([math]::Round($tbH * 0.19)))
$label.Dock = 'Fill'
$label.TextAlign = 'MiddleLeft'
[void]$form.Controls.Add($label)
Log 'step: label-ok'

$form.Add_HandleCreated({
    $GWL_EXSTYLE = -20
    $WS_EX_NOACTIVATE = 0x08000000
    $WS_EX_TOOLWINDOW = 0x00000080
    $SWP_NOSIZE = 0x0001; $SWP_NOMOVE = 0x0002; $SWP_SHOWWINDOW = 0x0040
    $HWND_TOPMOST = [IntPtr](-1)
    $ex = [NativeMethods]::GetWindowLong($form.Handle, $GWL_EXSTYLE)
    [void][NativeMethods]::SetWindowLong($form.Handle, $GWL_EXSTYLE, ($ex -bor $WS_EX_NOACTIVATE -bor $WS_EX_TOOLWINDOW))
    [void][NativeMethods]::SetWindowPos($form.Handle, $HWND_TOPMOST, 0, 0, 0, 0, ($SWP_NOSIZE -bor $SWP_NOMOVE -bor $SWP_SHOWWINDOW))
    Log ("exstyle applied=0x{0:x}" -f ([NativeMethods]::GetWindowLong($form.Handle, $GWL_EXSTYLE)))
})

$marquee = New-Object System.Windows.Forms.Timer
$marquee.Interval = 120
$marquee.Add_Tick({
    if ($label.Text.Length -gt 1) {
        $label.Text = $label.Text.Substring(1) + $label.Text.Substring(0, 1)
    }
})

$beat = New-Object System.Windows.Forms.Timer
$beat.Interval = 2000
$beat.Add_Tick({
    if ($form.WindowState -eq 'Minimized') {
        $form.WindowState = 'Normal'
        [void][NativeMethods]::SetWindowPos($form.Handle, [IntPtr](-1), 0, 0, 0, 0, (0x0003 -bor 0x0040))
        Log 'restored-after-minimize'
    }
    Log ("beat visible={0} bounds={1} text_len={2}" -f $form.Visible, $form.Bounds.ToString(), $label.Text.Length)
})

$form.Add_Shown({ $marquee.Start(); $beat.Start(); Log 'shown' })
Log ('starting v2, pid=' + $PID)
[System.Windows.Forms.Application]::Run($form)
Log 'exit'
