#!/usr/bin/env bash

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RELEASE_DIR="${HERE}/.."

fail() {
    echo "FAIL: $*" >&2
    exit 1
}

for script in community-compose-notes.sh community-create-release.sh; do
    [ -f "${RELEASE_DIR}/${script}" ] || fail "Missing ${RELEASE_DIR}/${script}"
done

TEST_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/lyricsx-community-publish.XXXXXX")"
trap 'rm -rf "$TEST_ROOT"' EXIT

FIXTURE="${TEST_ROOT}/fixture"
CAPTURE="${FIXTURE}/capture"
mkdir -p "${FIXTURE}/Scripts/release" "${FIXTURE}/ReleaseNotes" \
    "${FIXTURE}/build" "$CAPTURE"
cp "${RELEASE_DIR}/community-compose-notes.sh" \
    "${RELEASE_DIR}/community-create-release.sh" \
    "${RELEASE_DIR}/lib.sh" "${FIXTURE}/Scripts/release/"

printf '# English changes\n- Fixed transitions.\n' > "${FIXTURE}/ReleaseNotes/1.8.5_en.md"
printf '# Chinese changes\n- Improved lyrics.\n' > "${FIXTURE}/ReleaseNotes/1.8.5_zh.md"

VERSION='1.8.5-community.1'
BUILD='2924'
for suffix in zip dmg dSYMs.zip; do
    printf '%s artifact\n' "$suffix" > "${FIXTURE}/build/LyricsX_${VERSION}+${BUILD}.${suffix}"
done

(
    cd "$FIXTURE"
    export TEST_CAPTURE="$CAPTURE"

    hdiutil() {
        printf '%s\n' "$*" > "${TEST_CAPTURE}/hdiutil.call"
        [ "$1" = "verify" ]
    }

    gh() {
        printf '%s\n' "$@" > "${TEST_GH_ARGS}"
    }

    export -f hdiutil gh
    VERSION="$VERSION" BASE_VERSION='1.8.5' \
        bash Scripts/release/community-compose-notes.sh
    export TEST_GH_ARGS="${TEST_CAPTURE}/gh.with-target.args"
    VERSION="$VERSION" BUILD="$BUILD" GITHUB_SHA='abc123' \
        bash Scripts/release/community-create-release.sh
    export TEST_GH_ARGS="${TEST_CAPTURE}/gh.without-target.args"
    VERSION="$VERSION" BUILD="$BUILD" \
        bash Scripts/release/community-create-release.sh
)

BODY="${FIXTURE}/build/body.md"
CHECKSUM="${FIXTURE}/build/LyricsX_${VERSION}+${BUILD}.sha256"

for expected in \
    '# English changes' \
    '# Chinese changes' \
    'Open the DMG and drag LyricsX to Applications.' \
    '打开 DMG，然后将 LyricsX 拖到“应用程序”文件夹。' \
    'Privacy & Security' \
    'Open Anyway' \
    '隐私与安全性' \
    '仍要打开'; do
    grep -Fq "$expected" "$BODY" || fail "Release body is missing: ${expected}"
done

for prohibited in \
    'notarized' \
    'notarization' \
    'Apple verified' \
    'Apple approved' \
    '未经过 Apple 公证'; do
    if grep -Fiq "$prohibited" "$BODY"; then
        fail "Release body contains prohibited copy: ${prohibited}"
    fi
done

[ "$(wc -l < "$CHECKSUM" | tr -d ' ')" = "2" ] \
    || fail 'Checksum file must contain exactly two entries'
grep -Fq "LyricsX_${VERSION}+${BUILD}.zip" "$CHECKSUM" \
    || fail 'ZIP checksum is missing'
grep -Fq "LyricsX_${VERSION}+${BUILD}.dmg" "$CHECKSUM" \
    || fail 'DMG checksum is missing'
if grep -Fq 'dSYMs.zip' "$CHECKSUM"; then
    fail 'dSYMs must not appear in the end-user checksum file'
fi

GH_ARGS="${CAPTURE}/gh.with-target.args"
for expected in \
    'release' \
    'create' \
    "v${VERSION}" \
    '--prerelease' \
    '--notes-file' \
    'build/body.md' \
    "build/LyricsX_${VERSION}+${BUILD}.zip" \
    "build/LyricsX_${VERSION}+${BUILD}.dmg" \
    "build/LyricsX_${VERSION}+${BUILD}.dSYMs.zip" \
    "build/LyricsX_${VERSION}+${BUILD}.sha256"; do
    grep -Fqx -- "$expected" "$GH_ARGS" || fail "gh invocation is missing: ${expected}"
done
grep -Fqx -- '--target' "$GH_ARGS" || fail 'gh invocation is missing --target'
grep -Fqx -- 'abc123' "$GH_ARGS" || fail 'gh invocation is missing GITHUB_SHA'

if grep -Fqx -- '--target' "${CAPTURE}/gh.without-target.args"; then
    fail 'gh invocation must omit --target when GITHUB_SHA is empty'
fi

grep -Fqx -- "verify build/LyricsX_${VERSION}+${BUILD}.dmg" \
    "${CAPTURE}/hdiutil.call" || fail 'DMG verification was not invoked'

echo 'PASS: community notes, checksums, DMG verification, and prerelease upload are correct'
