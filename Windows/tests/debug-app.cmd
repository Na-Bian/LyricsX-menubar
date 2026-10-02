@echo off
del C:\Users\31576\testapp-log.txt 2>nul
del C:\Users\31576\probe2-out.txt 2>nul
start "" "C:\Users\31576\LyricsX\Windows\tests\LyricsX.Windows.TestMediaApp\bin\Debug\net10.0-windows10.0.22000.0\LyricsX.Windows.TestMediaApp.exe" --automation-pipe debugpipe
ping -n 6 127.0.0.1 >nul
echo === after 5s: >> C:\Users\31576\probe2-out.txt
tasklist /fi "imagename eq LyricsX.Windows.TestMediaApp.exe" >> C:\Users\31576\probe2-out.txt
"C:\Users\31576\LyricsX\Windows\tools\MediaSessionProbe\bin\Debug\net10.0-windows10.0.22000.0\MediaSessionProbe.exe" >> C:\Users\31576\probe2-out.txt 2>&1
ping -n 6 127.0.0.1 >nul
echo === after probe: >> C:\Users\31576\probe2-out.txt
tasklist /fi "imagename eq LyricsX.Windows.TestMediaApp.exe" >> C:\Users\31576\probe2-out.txt
type C:\Users\31576\testapp-log.txt >> C:\Users\31576\probe2-out.txt 2>nul
