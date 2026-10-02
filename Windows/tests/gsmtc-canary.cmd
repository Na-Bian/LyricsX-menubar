@echo off
powershell -NoProfile -Command "$sampleRate=8000;$sec=120;$dataSize=$sampleRate*$sec;$ms=New-Object System.IO.MemoryStream;$w=New-Object System.IO.BinaryWriter($ms);$w.Write([System.Text.Encoding]::ASCII.GetBytes('RIFF'));$w.Write(36+$dataSize);$w.Write([System.Text.Encoding]::ASCII.GetBytes('WAVEfmt '));$w.Write(16);$w.Write([short]1);$w.Write([short]1);$w.Write($sampleRate);$w.Write($sampleRate);$w.Write([short]1);$w.Write([short]8);$w.Write([System.Text.Encoding]::ASCII.GetBytes('data'));$w.Write($dataSize);$w.Write((New-Object byte[] $dataSize));$w.Flush();[System.IO.File]::WriteAllBytes('C:\Users\31576\gsmtc-canary.wav',$ms.ToArray())"
start "" "C:\Users\31576\gsmtc-canary.wav"
ping -n 12 127.0.0.1 >nul
"C:\Users\31576\LyricsX\Windows\tools\MediaSessionProbe\bin\Debug\net10.0-windows10.0.22000.0\MediaSessionProbe.exe" > C:\Users\31576\canary-out.txt 2>&1
tasklist >> C:\Users\31576\canary-out.txt
