# Captures the full physical screen (incl. taskbar) from the interactive session.
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Drawing

Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class DpiAware {
    [DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
    [DllImport("user32.dll")] public static extern int GetSystemMetrics(int index);
}
'@
[void][DpiAware]::SetProcessDPIAware()
$w = [DpiAware]::GetSystemMetrics(0)   # SM_CXSCREEN
$h = [DpiAware]::GetSystemMetrics(1)   # SM_CYSCREEN

$bmp = New-Object System.Drawing.Bitmap($w, $h)
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.CopyFromScreen(0, 0, 0, 0, (New-Object System.Drawing.Size($w, $h)))
$out = 'C:\Users\31576\spike-shot.png'
$bmp.Save($out, [System.Drawing.Imaging.ImageFormat]::Png)
$g.Dispose(); $bmp.Dispose()
("shot {0}x{1}" -f $w, $h) | Out-File -Append -FilePath (Join-Path $env:TEMP 'lyricsx-spike.log')
