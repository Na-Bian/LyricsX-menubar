#!/usr/bin/env bash
# Stage and apply the branded Finder layout for the LyricsX DMG.

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "${HERE}/lib.sh"

usage() {
    die "Usage: $0 prepare <stage-dir> | apply <mounted-volume>"
}

[ "$#" -eq 2 ] || usage

ACTION="$1"
DMG_ROOT="$2"
BACKGROUND_NAME='LyricsX-dmg-background.png'
BACKGROUND_SOURCE="${HERE}/dmg-assets/${BACKGROUND_NAME}"

[ -d "$DMG_ROOT" ] || die "DMG directory does not exist: ${DMG_ROOT}"

case "$ACTION" in
    prepare)
        [ -f "$BACKGROUND_SOURCE" ] || die "Missing DMG background: ${BACKGROUND_SOURCE}"
        mkdir -p "${DMG_ROOT}/.background"
        ditto "$BACKGROUND_SOURCE" "${DMG_ROOT}/.background/${BACKGROUND_NAME}"
        chflags hidden "${DMG_ROOT}/.background" 2>/dev/null || true
        log_info "Staged branded DMG background"
        ;;
    apply)
        OSASCRIPT_BIN="${OSASCRIPT_BIN:-/usr/bin/osascript}"
        if [ ! -x "$OSASCRIPT_BIN" ]; then
            log_warn "Finder layout tool unavailable; keeping the standard drag-install DMG"
            exit 0
        fi

        MOUNTED_DISK_NAME="$(basename "$DMG_ROOT")"
        if ! "$OSASCRIPT_BIN" - "$MOUNTED_DISK_NAME" <<'APPLESCRIPT'
on run argv
    set mountedDiskName to item 1 of argv
    tell application "Finder"
        set targetDisk to disk mountedDiskName
        open targetDisk
        delay 1
        set targetWindow to container window of targetDisk
        set current view of targetWindow to icon view
        set toolbar visible of targetWindow to false
        set statusbar visible of targetWindow to false
        set pathbar visible of targetWindow to false
        set bounds of targetWindow to {100, 100, 740, 500}
        set viewOptions to icon view options of targetWindow
        set arrangement of viewOptions to not arranged
        set icon size of viewOptions to 112
        set text size of viewOptions to 13
        set background picture of viewOptions to file ".background:LyricsX-dmg-background.png" of targetDisk
        set position of item "LyricsX.app" of targetDisk to {165, 220}
        set position of item "Applications" of targetDisk to {475, 220}
        update targetDisk without registering applications
        delay 2
        close targetWindow
    end tell
end run
APPLESCRIPT
        then
            log_warn "Finder layout could not be applied; keeping the standard drag-install DMG"
            exit 0
        fi

        log_info "Applied branded Finder layout"
        ;;
    *)
        usage
        ;;
esac
