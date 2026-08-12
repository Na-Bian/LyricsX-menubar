#!/usr/bin/env bash
# Produce the app ZIP, drag-install DMG, and dSYMs ZIP.
#
# Inputs (env):
#   VERSION, BUILD

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "${HERE}/lib.sh"
cd "$(repo_root)"

require_env VERSION BUILD

APP_PATH="build/Export/LyricsX.app"
DSYMS_DIR="build/LyricsX.xcarchive/dSYMs"
APP_ZIP="build/LyricsX_${VERSION}+${BUILD}.zip"
APP_DMG="build/LyricsX_${VERSION}+${BUILD}.dmg"
DSYMS_ZIP="build/LyricsX_${VERSION}+${BUILD}.dSYMs.zip"
DMG_STAGE_DIR="$(mktemp -d "${TMPDIR:-/tmp}/LyricsX.dmg.XXXXXX")"
DMG_MOUNT_DIR="$(mktemp -d "${TMPDIR:-/tmp}/LyricsX.mount.XXXXXX")"
DMG_RW="build/LyricsX.dmg.rw"
DMG_ATTACHED=0

cleanup() {
    if [ "$DMG_ATTACHED" = "1" ]; then
        hdiutil detach "$DMG_MOUNT_DIR" >/dev/null 2>&1 || true
    fi
    rm -rf "$DMG_STAGE_DIR" "$DMG_MOUNT_DIR"
    rm -f "$DMG_RW"
}
trap cleanup EXIT

[ -d "$APP_PATH" ]   || die "Expected ${APP_PATH}"
[ -d "$DSYMS_DIR" ]  || die "Expected ${DSYMS_DIR}"
if [ -z "$(ls -A "$DSYMS_DIR" 2>/dev/null)" ]; then
    die "${DSYMS_DIR} is empty — no dSYMs were produced"
fi

log_info "Packaging app → ${APP_ZIP}"
rm -f "$APP_ZIP"
ditto -c -k --sequesterRsrc --keepParent "$APP_PATH" "$APP_ZIP"

log_info "Packaging drag-install DMG → ${APP_DMG}"
ditto "$APP_PATH" "$DMG_STAGE_DIR/LyricsX.app"
ln -s /Applications "$DMG_STAGE_DIR/Applications"
bash "${HERE}/community-dmg-layout.sh" prepare "$DMG_STAGE_DIR"
rm -f "$APP_DMG" "$DMG_RW"
hdiutil create -volname "LyricsX" -srcfolder "$DMG_STAGE_DIR" -ov -format UDRW "$DMG_RW"
hdiutil attach -readwrite -noverify -noautoopen -mountpoint "$DMG_MOUNT_DIR" "$DMG_RW"
DMG_ATTACHED=1
bash "${HERE}/community-dmg-layout.sh" apply "$DMG_MOUNT_DIR"
hdiutil detach "$DMG_MOUNT_DIR"
DMG_ATTACHED=0
hdiutil convert "$DMG_RW" -format UDZO -o "$APP_DMG"

log_info "Packaging dSYMs → ${DSYMS_ZIP}"
rm -f "$DSYMS_ZIP"
ditto -c -k --sequesterRsrc --keepParent "$DSYMS_DIR" "$DSYMS_ZIP"

log_info "Produced:"
ls -lh "$APP_ZIP" "$APP_DMG" "$DSYMS_ZIP"
