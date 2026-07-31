#!/usr/bin/env bash
# Exercise build.sh with and without API credentials using a fake xcodebuild.

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
RELEASE_DIR="${HERE}/.."

fail() {
    echo "FAIL: $*" >&2
    exit 1
}

[ -f "${RELEASE_DIR}/build.sh" ] || fail "Missing ${RELEASE_DIR}/build.sh"
[ -f "${RELEASE_DIR}/lib.sh" ] || fail "Missing ${RELEASE_DIR}/lib.sh"

TEST_ROOT="$(mktemp -d "${TMPDIR:-/tmp}/lyricsx-build-test.XXXXXX")"
trap 'rm -rf "$TEST_ROOT"' EXIT

assert_contains() {
    local file="$1"
    local expected="$2"
    grep -Fqx -- "$expected" "$file" || fail "Expected ${expected} in ${file}"
}

assert_absent() {
    local file="$1"
    local unexpected="$2"
    if grep -Fqx -- "$unexpected" "$file"; then
        fail "Did not expect ${unexpected} in ${file}"
    fi
}

run_case() {
    local case_name="$1"
    local include_api_key="$2"
    local case_root="${TEST_ROOT}/${case_name}"
    local capture_dir="${case_root}/capture"

    mkdir -p "${case_root}/Scripts/release" "$capture_dir"
    cp "${RELEASE_DIR}/build.sh" "${RELEASE_DIR}/lib.sh" "${case_root}/Scripts/release/"

    (
        cd "$case_root"
        export TEST_CAPTURE_DIR="$capture_dir"
        export TEST_XCODEBUILD_CALL=0
        xcodebuild() {
            local argument
            local call_file
            local is_export=0

            TEST_XCODEBUILD_CALL=$((TEST_XCODEBUILD_CALL + 1))
            call_file="${TEST_CAPTURE_DIR}/${TEST_XCODEBUILD_CALL}.args"
            printf '%s\n' "$@" > "$call_file"

            for argument in "$@"; do
                [ "$argument" = "-exportArchive" ] && is_export=1
            done
            if [ "$is_export" = "1" ]; then
                mkdir -p build/Export/LyricsX.app
            else
                mkdir -p build/LyricsX.xcarchive/dSYMs
            fi
        }

        if [ "$include_api_key" = "true" ]; then
            export APPLE_API_KEY_P8_BASE64="$(printf test-api-key | base64)"
            export APPLE_API_KEY_ID="ABCDEFGHIJ"
            export APPLE_API_KEY_ISSUER_ID="12345678-1234-1234-1234-123456789abc"
        else
            unset APPLE_API_KEY_P8_BASE64 APPLE_API_KEY_ID APPLE_API_KEY_ISSUER_ID
        fi

        source Scripts/release/build.sh
    )

    printf '%s' "$capture_dir"
}

NO_API_CAPTURE="$(run_case no-api false)"
[ -f "${NO_API_CAPTURE}/1.args" ] || fail "No-API archive call was not captured"
[ -f "${NO_API_CAPTURE}/2.args" ] || fail "No-API export call was not captured"
assert_absent "${NO_API_CAPTURE}/1.args" "-allowProvisioningUpdates"
assert_absent "${NO_API_CAPTURE}/2.args" "-allowProvisioningUpdates"

API_CAPTURE="$(run_case with-api true)"
for args_file in "${API_CAPTURE}/1.args" "${API_CAPTURE}/2.args"; do
    assert_contains "$args_file" "-allowProvisioningUpdates"
    assert_contains "$args_file" "-authenticationKeyID"
    assert_contains "$args_file" "ABCDEFGHIJ"
    assert_contains "$args_file" "-authenticationKeyIssuerID"
    assert_contains "$args_file" "12345678-1234-1234-1234-123456789abc"
done

echo "PASS: build.sh handles empty and populated AUTH_ARGS with bash nounset enabled"
