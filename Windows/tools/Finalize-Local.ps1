$ErrorActionPreference = 'Continue'
$log = 'C:\Users\31576\finalize.log'
"begin " + (Get-Date) | Out-File $log

Set-Location C:\Users\31576\LyricsX\Windows
& .\tools\Publish-Release.ps1 -Version 0.1.0 2>&1 | Out-File $log -Append
& .\tools\Build-Installer.ps1 -Version 0.1.0 -Arch x64 2>&1 | Out-File $log -Append
& .\tools\Build-Installer.ps1 -Version 0.1.0 -Arch arm64 2>&1 | Out-File $log -Append
& .\tools\Test-InstallerLifecycle.ps1 -Installer C:\Users\31576\LyricsX\Windows\out\installer\LyricsX-Windows-Setup-x64.exe 2>&1 | Out-File $log -Append
Add-Content $log "ALL-DONE"
