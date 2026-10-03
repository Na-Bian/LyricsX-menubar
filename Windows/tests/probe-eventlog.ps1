$ErrorActionPreference = 'Continue'
$out = 'C:\Users\31576\eventlog-dump.txt'
Remove-Item $out -ErrorAction SilentlyContinue
$events = Get-WinEvent -LogName Application -MaxEvents 100 |
    Where-Object { $_.Message -match 'LyricsX' } |
    Select-Object -First 4
foreach ($e in $events) {
    ("=== " + $e.TimeCreated + " [" + $e.ProviderName + "]") | Out-File $out -Append
    $e.Message.Substring(0, [Math]::Min(500, $e.Message.Length)) | Out-File $out -Append
}
"done" | Out-File $out -Append
