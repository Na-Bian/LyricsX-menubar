#!/bin/bash
# VM build loop helper for the Windows port.
# usage: vm.sh sync | build | test <dotnet-test-args...>
# All dotnet work runs on the Windows VM; this Mac has no SDK.
set -e
VM=31576@192.168.213.128
ROOT=/Users/nabian/Documents/LyricsX-windows

do_sync() {
  ssh -o BatchMode=yes $VM "if not exist C:\\Users\\31576\\LyricsX mkdir C:\\Users\\31576\\LyricsX" >/dev/null
  COPYFILE_DISABLE=1 tar -czf - -C "$ROOT" --exclude '._*' Windows | ssh -o BatchMode=yes $VM "tar -xzf - -C C:/Users/31576/LyricsX"
}

case "${1:-}" in
  sync)
    do_sync
    echo "synced" ;;
  build)
    do_sync
    ssh -o BatchMode=yes $VM "cd /d C:\\Users\\31576\\LyricsX\\Windows && dotnet build LyricsX.Windows.slnx --no-restore 2>&1" ;;
  test)
    shift
    do_sync
    ssh -o BatchMode=yes $VM "cd /d C:\\Users\\31576\\LyricsX\\Windows && dotnet test $* 2>&1" ;;
  testplat)
    # platform-suite filter via .rsp file (avoids cmd-over-ssh quoting)
    do_sync
    ssh -o BatchMode=yes $VM "powershell -NoProfile -Command \"Set-Location C:\Users\31576\LyricsX\Windows; dotnet test tests/LyricsX.Platform.Windows.Tests/LyricsX.Platform.Windows.Tests.csproj @C:\Users\31576\testfilter.rsp 2>&1\"" ;;
  regress)
    # full offline regression across the solution
    do_sync
    ssh -o BatchMode=yes $VM "powershell -NoProfile -Command \"Set-Location C:\Users\31576\LyricsX\Windows; dotnet test LyricsX.Windows.slnx @C:\Users\31576\testfilter.rsp 2>&1\"" ;;
  *)
    echo "usage: vm.sh sync|build|test <args>" >&2
    exit 2 ;;
esac
