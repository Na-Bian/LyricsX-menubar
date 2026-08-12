#!/usr/bin/env bash

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RELEASE_DIR="${HERE}/.."

fail() {
    echo "FAIL: $*" >&2
    exit 1
}

[ -f "${RELEASE_DIR}/community-build.sh" ] \
    || fail "Missing ${RELEASE_DIR}/community-build.sh"

TEST_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/lyricsx-community-build.XXXXXX")"
trap 'rm -rf "$TEST_ROOT"' EXIT

FIXTURE="${TEST_ROOT}/fixture"
CAPTURE="${FIXTURE}/capture"
mkdir -p "${FIXTURE}/Scripts/release" "$CAPTURE"
cp "${RELEASE_DIR}/community-build.sh" "${RELEASE_DIR}/lib.sh" \
    "${FIXTURE}/Scripts/release/"

(
    cd "$FIXTURE"
    export TEST_CAPTURE="$CAPTURE"

    xcodebuild() {
        local derived_data=""
        local previous=""
        local argument

        printf '%s\n' "$@" > "${TEST_CAPTURE}/xcodebuild.args"
        for argument in "$@"; do
            if [ "$previous" = "-derivedDataPath" ]; then
                derived_data="$argument"
            fi
            previous="$argument"
        done
        [ -n "$derived_data" ] || return 91

        mkdir -p \
            "${derived_data}/Build/Products/Release/LyricsX.app/Contents/MacOS" \
            "${derived_data}/Build/Products/Release/LyricsX.app.dSYM/Contents/Resources/DWARF" \
            "${derived_data}/Build/Products/Release/LyricsXHelper.app.dSYM/Contents/Resources/DWARF"
        printf 'app' > "${derived_data}/Build/Products/Release/LyricsX.app/Contents/MacOS/LyricsX"
    }

    ditto() {
        printf '%s\n' "$@" >> "${TEST_CAPTURE}/ditto.args"
        cp -R "$1" "$2"
    }

    codesign() {
        printf '%s\n' "$*" >> "${TEST_CAPTURE}/codesign.calls"
    }

    source Scripts/release/community-build.sh
)

for expected in \
    'CODE_SIGNING_ALLOWED=NO' \
    'CODE_SIGNING_REQUIRED=NO' \
    'CODE_SIGN_IDENTITY='; do
    grep -Fqx -- "$expected" "${CAPTURE}/xcodebuild.args" \
        || fail "Missing xcodebuild argument: ${expected}"
done

[ -d "${FIXTURE}/build/Export/LyricsX.app" ] \
    || fail 'Exported LyricsX.app was not produced'
[ -d "${FIXTURE}/build/LyricsX.xcarchive/dSYMs/LyricsX.app.dSYM" ] \
    || fail 'LyricsX dSYM was not collected'
[ -d "${FIXTURE}/build/LyricsX.xcarchive/dSYMs/LyricsXHelper.app.dSYM" ] \
    || fail 'LyricsXHelper dSYM was not collected'

grep -Fqx -- '--force --deep --sign - --timestamp=none build/Export/LyricsX.app' \
    "${CAPTURE}/codesign.calls" || fail 'Ad-hoc signing call was not captured'
grep -Fqx -- '--verify --deep --strict --verbose=2 build/Export/LyricsX.app' \
    "${CAPTURE}/codesign.calls" || fail 'Signature verification call was not captured'

echo 'PASS: community build disables normal signing and verifies its ad-hoc signature'
