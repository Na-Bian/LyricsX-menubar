@echo off
cd /d C:\Users\31576\LyricsX\Windows
"C:\Program Files\dotnet\dotnet.exe" test tests\LyricsX.Platform.Windows.Tests\LyricsX.Platform.Windows.Tests.csproj --filter TestCategory=WindowsIntegration --no-build --logger "console;verbosity=detailed" > C:\Users\31576\integ-out.txt 2>&1
