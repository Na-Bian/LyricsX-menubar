#!/usr/bin/env bash
# Verify the release scripts retain the drag-install DMG packaging contract.

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PACKAGE_SCRIPT="${HERE}/../package.sh"
RELEASE_SCRIPT="${HERE}/../create-release.sh"

fail() {
    echo "FAIL: $*" >&2
    exit 1
}

[ -f "$PACKAGE_SCRIPT" ] || fail "Missing ${PACKAGE_SCRIPT}"
[ -f "$RELEASE_SCRIPT" ] || fail "Missing ${RELEASE_SCRIPT}"

grep -Fq 'APP_DMG="build/LyricsX_${VERSION}+${BUILD}.dmg"' "$PACKAGE_SCRIPT" \
    || fail "package.sh must declare APP_DMG"
grep -Fq 'DMG_STAGE_DIR="$(mktemp -d "${TMPDIR:-/tmp}/LyricsX.dmg.XXXXXX")"' "$PACKAGE_SCRIPT" \
    || fail "package.sh must create a temporary DMG staging directory"
grep -Fq 'ln -s /Applications "$DMG_STAGE_DIR/Applications"' "$PACKAGE_SCRIPT" \
    || fail "package.sh must stage an Applications symlink"
grep -Fq 'community-dmg-layout.sh" prepare "$DMG_STAGE_DIR"' "$PACKAGE_SCRIPT" \
    || fail "package.sh must stage the branded DMG layout"
grep -Fq 'DMG_RW="build/LyricsX.dmg.rw"' "$PACKAGE_SCRIPT" \
    || fail "package.sh must declare a writable DMG image"
grep -Fq 'DMG_MOUNT_DIR="$(mktemp -d' "$PACKAGE_SCRIPT" \
    || fail "package.sh must declare a DMG mount directory"
grep -Fq 'hdiutil create -volname "LyricsX" -srcfolder "$DMG_STAGE_DIR" -ov -format UDRW "$DMG_RW"' "$PACKAGE_SCRIPT" \
    || fail "package.sh must create a writable LyricsX DMG"
grep -Fq 'hdiutil attach -readwrite -noverify -noautoopen -mountpoint "$DMG_MOUNT_DIR" "$DMG_RW"' "$PACKAGE_SCRIPT" \
    || fail "package.sh must mount the writable DMG"
grep -Fq 'community-dmg-layout.sh" apply "$DMG_MOUNT_DIR"' "$PACKAGE_SCRIPT" \
    || fail "package.sh must apply the branded Finder layout"
grep -Fq 'hdiutil detach "$DMG_MOUNT_DIR"' "$PACKAGE_SCRIPT" \
    || fail "package.sh must detach the writable DMG"
grep -Fq 'hdiutil convert "$DMG_RW" -format UDZO -o "$APP_DMG"' "$PACKAGE_SCRIPT" \
    || fail "package.sh must convert the writable DMG to UDZO"
grep -Fq 'APP_DMG="build/LyricsX_${VERSION}+${BUILD}.dmg"' "$RELEASE_SCRIPT" \
    || fail "create-release.sh must declare APP_DMG"
grep -Fq '[ -f "$APP_DMG" ]' "$RELEASE_SCRIPT" \
    || fail "create-release.sh must validate APP_DMG"
grep -Fq '    "$APP_DMG"' "$RELEASE_SCRIPT" \
    || fail "create-release.sh must upload APP_DMG"

echo "PASS: DMG packaging and release upload wiring are present"
