# Sends a real Win+D keystroke from the interactive session.
$ErrorActionPreference = 'Stop'
Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class KeySend {
    [DllImport("user32.dll")] public static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);
}
'@
$VK_LWIN = 0x5B; $VK_D = 0x44
$KEYEVENTF_KEYUP = 0x0002
[KeySend]::keybd_event($VK_LWIN, 0, 0, [UIntPtr]::Zero)
[KeySend]::keybd_event($VK_D, 0, 0, [UIntPtr]::Zero)
[KeySend]::keybd_event($VK_D, 0, $KEYEVENTF_KEYUP, [UIntPtr]::Zero)
[KeySend]::keybd_event($VK_LWIN, 0, $KEYEVENTF_KEYUP, [UIntPtr]::Zero)
'win-d sent' | Out-File -Append -FilePath (Join-Path $env:TEMP 'lyricsx-spike.log')
