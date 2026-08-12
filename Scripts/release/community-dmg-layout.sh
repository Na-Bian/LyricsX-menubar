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

        if ! "$OSASCRIPT_BIN" - "$DMG_ROOT" <<'APPLESCRIPT'
on run argv
    set volumePath to item 1 of argv
    tell application "Finder"
        set dmgFolder to POSIX file volumePath as alias
        open dmgFolder
        tell container window of dmgFolder
            set current view to icon view
            set toolbar visible to false
            set statusbar visible to false
            set pathbar visible to false
            set bounds of container window of dmgFolder to {100, 100, 740, 500}
            set arrangement of icon view options to not arranged
            set icon size of icon view options to 112
            set text size of icon view options to 13
            set background picture of icon view options to file ".background:LyricsX-dmg-background.png"
            set position of item "LyricsX.app" to {165, 220}
            set position of item "Applications" to {475, 220}
        end tell
        close container window of dmgFolder
        open dmgFolder
        update dmgFolder without registering applications
        delay 2
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
