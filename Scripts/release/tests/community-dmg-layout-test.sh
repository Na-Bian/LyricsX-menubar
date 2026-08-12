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
[ "$WIDTH" = '640' ] || fail "Expected 640px background width, got ${WIDTH}"
[ "$HEIGHT" = '400' ] || fail "Expected 400px background height, got ${HEIGHT}"

CAPTURE="${TEST_ROOT}/finder-script.applescript"
FAKE_OSASCRIPT="${TEST_ROOT}/osascript"
cat > "$FAKE_OSASCRIPT" <<'FAKE'
#!/usr/bin/env bash
cat > "$TEST_CAPTURE"
exit 23
FAKE
chmod +x "$FAKE_OSASCRIPT"

TEST_CAPTURE="$CAPTURE" OSASCRIPT_BIN="$FAKE_OSASCRIPT" \
    bash "$HELPER" apply "$STAGE"

for expected in \
    'set background picture of icon view options' \
    'set position of item "LyricsX.app" to {165, 220}' \
    'set position of item "Applications" to {475, 220}' \
    'set icon size of icon view options to 112' \
    'set bounds of container window of dmgFolder to {100, 100, 740, 500}'; do
    grep -Fq "$expected" "$CAPTURE" \
        || fail "Finder layout is missing: ${expected}"
done

[ -d "${STAGE}/LyricsX.app" ] || fail 'apply removed LyricsX.app after Finder failure'
[ -L "${STAGE}/Applications" ] || fail 'apply removed Applications after Finder failure'

echo 'PASS: DMG background and Finder layout degrade safely'
