$ErrorActionPreference = 'Continue'
$log = 'C:\Users\31576\rebuild.log'
Set-Location C:\Users\31576\LyricsX\Windows
"begin" | Out-File $log
& .\tools\Publish-Release.ps1 -Version 0.1.0 2>&1 | Out-File $log -Append
& .\tools\Build-Installer.ps1 -Version 0.1.0 -Arch x64 2>&1 | Out-File $log -Append
& .\tools\Test-InstallerLifecycle.ps1 -Installer C:\Users\31576\LyricsX\Windows\out\installer\LyricsX-Windows-Setup-x64.exe 2>&1 | Out-File $log -Append
Stop-Process -Name LyricsX.Windows.App -ErrorAction SilentlyContinue
Start-Process "$env:LOCALAPPDATA\Programs\LyricsX\LyricsX.Windows.App.exe"
Start-Sleep 12
$alive = Get-Process LyricsX.Windows.App -ErrorAction SilentlyContinue
if ($alive) { Add-Content $log "APP-ALIVE" } else { Add-Content $log "APP-DEAD" }
Add-Content $log "REBUILD-DONE"
