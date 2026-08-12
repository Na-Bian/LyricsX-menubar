#!/usr/bin/env bash
# Verify community artifacts and publish a GitHub prerelease.
#
# Inputs (env): VERSION, BUILD, GITHUB_SHA (optional), GH_TOKEN or GITHUB_TOKEN

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "${HERE}/lib.sh"
cd "$(repo_root)"

require_env VERSION BUILD

APP_ZIP="build/LyricsX_${VERSION}+${BUILD}.zip"
APP_DMG="build/LyricsX_${VERSION}+${BUILD}.dmg"
DSYMS_ZIP="build/LyricsX_${VERSION}+${BUILD}.dSYMs.zip"
CHECKSUM="build/LyricsX_${VERSION}+${BUILD}.sha256"
BODY="build/body.md"

[ -f "$APP_ZIP" ] || die "Missing ${APP_ZIP}"
[ -f "$APP_DMG" ] || die "Missing ${APP_DMG}"
[ -f "$DSYMS_ZIP" ] || die "Missing ${DSYMS_ZIP}"
[ -f "$BODY" ] || die "Missing ${BODY}"
command -v gh >/dev/null 2>&1 || die 'GitHub CLI (gh) is required'

log_info "Generating checksums"
shasum -a 256 "$APP_ZIP" "$APP_DMG" > "$CHECKSUM"

log_info "Verifying ${APP_DMG}"
hdiutil verify "$APP_DMG"

TARGET_ARGS=()
if [ -n "${GITHUB_SHA:-}" ]; then
    TARGET_ARGS+=(--target "$GITHUB_SHA")
fi

log_info "Creating GitHub prerelease v${VERSION}"
gh release create "v${VERSION}" \
    --prerelease \
    ${TARGET_ARGS[@]+"${TARGET_ARGS[@]}"} \
    --title "LyricsX ${VERSION}" \
    --notes-file "$BODY" \
    "$APP_ZIP" \
    "$APP_DMG" \
    "$DSYMS_ZIP" \
    "$CHECKSUM"

log_info "Published prerelease v${VERSION}"
