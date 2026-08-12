#!/usr/bin/env bash

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RELEASE_DIR="${HERE}/.."
HELPER="${RELEASE_DIR}/community-dmg-layout.sh"
BACKGROUND="${RELEASE_DIR}/dmg-assets/LyricsX-dmg-background.png"

fail() {
    echo "FAIL: $*" >&2
    exit 1
}

[ -f "$HELPER" ] || fail "Missing ${HELPER}"
[ -f "$BACKGROUND" ] || fail "Missing ${BACKGROUND}"

TEST_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/lyricsx-dmg-layout.XXXXXX")"
trap 'rm -rf "$TEST_ROOT"' EXIT

STAGE="${TEST_ROOT}/LyricsX"
mkdir -p "${STAGE}/LyricsX.app"
ln -s /Applications "${STAGE}/Applications"

bash "$HELPER" prepare "$STAGE"

STAGED_BACKGROUND="${STAGE}/.background/LyricsX-dmg-background.png"
[ -f "$STAGED_BACKGROUND" ] || fail 'prepare did not stage the DMG background'
[ -L "${STAGE}/Applications" ] || fail 'prepare replaced the Applications symlink'
[ "$(readlink "${STAGE}/Applications")" = '/Applications' ] \
    || fail 'Applications symlink target changed'

WIDTH="$(sips -g pixelWidth "$STAGED_BACKGROUND" | awk '/pixelWidth/ { print $2 }')"
HEIGHT="$(sips -g pixelHeight "$STAGED_BACKGROUND" | awk '/pixelHeight/ { print $2 }')"
DPI_WIDTH="$(sips -g dpiWidth "$STAGED_BACKGROUND" | awk '/dpiWidth/ { print $2 }')"
DPI_HEIGHT="$(sips -g dpiHeight "$STAGED_BACKGROUND" | awk '/dpiHeight/ { print $2 }')"
[ "$WIDTH" = '1280' ] || fail "Expected 1280px Retina background width, got ${WIDTH}"
[ "$HEIGHT" = '800' ] || fail "Expected 800px Retina background height, got ${HEIGHT}"
[ "$DPI_WIDTH" = '144.000' ] || fail "Expected 144 DPI width, got ${DPI_WIDTH}"
[ "$DPI_HEIGHT" = '144.000' ] || fail "Expected 144 DPI height, got ${DPI_HEIGHT}"

CAPTURE="${TEST_ROOT}/finder-script.applescript"
ARGS_CAPTURE="${TEST_ROOT}/osascript.args"
FAKE_OSASCRIPT="${TEST_ROOT}/osascript"
cat > "$FAKE_OSASCRIPT" <<'FAKE'
#!/usr/bin/env bash
printf '%s\n' "$@" > "$TEST_ARGS_CAPTURE"
cat > "$TEST_CAPTURE"
exit 23
FAKE
chmod +x "$FAKE_OSASCRIPT"

TEST_CAPTURE="$CAPTURE" TEST_ARGS_CAPTURE="$ARGS_CAPTURE" \
    OSASCRIPT_BIN="$FAKE_OSASCRIPT" \
    bash "$HELPER" apply "$STAGE"

for expected in \
    'set targetDisk to disk mountedDiskName' \
    'set targetWindow to container window of targetDisk' \
    'set viewOptions to icon view options of targetWindow' \
    'set background picture of viewOptions to file ".background:LyricsX-dmg-background.png" of targetDisk' \
    'set position of item "LyricsX.app" of targetDisk to {165, 220}' \
    'set position of item "Applications" of targetDisk to {475, 220}' \
    'set icon size of viewOptions to 112' \
    'set bounds of targetWindow to {100, 100, 740, 500}'; do
    grep -Fq "$expected" "$CAPTURE" \
        || fail "Finder layout is missing: ${expected}"
done

[ "$(sed -n '2p' "$ARGS_CAPTURE")" = "$(basename "$STAGE")" ] \
    || fail 'apply must identify the Finder disk by the mount directory basename'

[ -d "${STAGE}/LyricsX.app" ] || fail 'apply removed LyricsX.app after Finder failure'
[ -L "${STAGE}/Applications" ] || fail 'apply removed Applications after Finder failure'

echo 'PASS: DMG background and Finder layout degrade safely'
