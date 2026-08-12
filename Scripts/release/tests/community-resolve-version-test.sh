#!/usr/bin/env bash

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RELEASE_DIR="${HERE}/.."

fail() {
    echo "FAIL: $*" >&2
    exit 1
}

[ -f "${RELEASE_DIR}/community-resolve-version.sh" ] \
    || fail "Missing ${RELEASE_DIR}/community-resolve-version.sh"

TEST_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/lyricsx-community-version.XXXXXX")"
trap 'rm -rf "$TEST_ROOT"' EXIT

make_fixture() {
    local fixture="$1"

    mkdir -p "${fixture}/Scripts/release" "${fixture}/LyricsX/Supporting Files" \
        "${fixture}/ReleaseNotes"
    cp "${RELEASE_DIR}/community-resolve-version.sh" "${RELEASE_DIR}/lib.sh" \
        "${fixture}/Scripts/release/"
    cp "LyricsX/Supporting Files/Info.plist" \
        "${fixture}/LyricsX/Supporting Files/Info.plist"
    printf '# English notes\n' > "${fixture}/ReleaseNotes/1.8.5_en.md"
    printf '# Chinese notes\n' > "${fixture}/ReleaseNotes/1.8.5_zh.md"
}

run_resolver() {
    local fixture="$1"
    local version="$2"
    local env_file="${fixture}/github-env"

    (
        cd "$fixture"
        gh() { return 1; }
        export -f gh
        INPUT_VERSION="$version" GITHUB_ENV="$env_file" \
            bash Scripts/release/community-resolve-version.sh
    )
}

VALID_FIXTURE="${TEST_ROOT}/valid"
make_fixture "$VALID_FIXTURE"
VALID_OUTPUT="$(run_resolver "$VALID_FIXTURE" '1.8.5-community.1')"

for expected in \
    'VERSION=1.8.5-community.1' \
    'BASE_VERSION=1.8.5' \
    'BUILD=2924' \
    'IS_PRERELEASE=true'; do
    printf '%s\n' "$VALID_OUTPUT" | grep -Fqx "$expected" \
        || fail "Missing stdout value: ${expected}"
    grep -Fqx "$expected" "${VALID_FIXTURE}/github-env" \
        || fail "Missing GITHUB_ENV value: ${expected}"
done

assert_rejected() {
    local name="$1"
    local version="$2"
    local expected="$3"
    local fixture="${TEST_ROOT}/${name}"
    local output

    make_fixture "$fixture"
    if [ "$name" = "base-mismatch" ]; then
        /usr/libexec/PlistBuddy -c 'Set :CFBundleShortVersionString 1.8.4' \
            "${fixture}/LyricsX/Supporting Files/Info.plist"
    elif [ "$name" = "missing-notes" ]; then
        rm "${fixture}/ReleaseNotes/1.8.5_zh.md"
    fi

    if output="$(run_resolver "$fixture" "$version" 2>&1)"; then
        fail "Expected ${version} (${name}) to be rejected"
    fi
    printf '%s\n' "$output" | grep -Fq "$expected" \
        || fail "${name} did not report expected error: ${expected}"
}

assert_rejected no-suffix '1.8.5' 'Expected <base>-community.<positive integer>'
assert_rejected zero-sequence '1.8.5-community.0' 'Expected <base>-community.<positive integer>'
assert_rejected base-mismatch '1.8.5-community.1' "doesn't match community base version"
assert_rejected missing-notes '1.8.5-community.1' 'Missing release notes'

echo 'PASS: community release versions are resolved and validated'
