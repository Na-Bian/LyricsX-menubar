#!/usr/bin/env bash
# Resolve and validate a community release version against the app base version.
#
# Inputs (env):
#   INPUT_VERSION       e.g. 1.8.5-community.1
#   GITHUB_ENV          (optional) GitHub Actions environment file
#
# Outputs:
#   VERSION, BASE_VERSION, BUILD, and IS_PRERELEASE=true

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "${HERE}/lib.sh"
cd "$(repo_root)"

require_env INPUT_VERSION

VERSION="$INPUT_VERSION"
COMMUNITY_VERSION_REGEX='^([0-9]+\.[0-9]+\.[0-9]+)-community\.([1-9][0-9]*)$'
if ! [[ "$VERSION" =~ $COMMUNITY_VERSION_REGEX ]]; then
    die "Invalid community version '${VERSION}'. Expected <base>-community.<positive integer>, e.g. 1.8.5-community.1"
fi

BASE_VERSION="${BASH_REMATCH[1]}"
PLIST_VERSION="$(plist_buddy -c 'Print CFBundleShortVersionString' "$INFO_PLIST_PATH")"
if [ "$PLIST_VERSION" != "$BASE_VERSION" ]; then
    die "Info.plist CFBundleShortVersionString ('${PLIST_VERSION}') doesn't match community base version ('${BASE_VERSION}')"
fi

BUILD="$(plist_buddy -c 'Print CFBundleVersion' "$INFO_PLIST_PATH")"
if ! [[ "$BUILD" =~ ^[1-9][0-9]*$ ]]; then
    die "Info.plist CFBundleVersion ('${BUILD}') is not a positive integer"
fi

for language in en zh; do
    notes="ReleaseNotes/${BASE_VERSION}_${language}.md"
    [ -f "$notes" ] || die "Missing release notes: ${notes}"
done

if command -v gh >/dev/null 2>&1 && gh release view "v${VERSION}" >/dev/null 2>&1; then
    die "Release v${VERSION} already exists"
fi

IS_PRERELEASE="true"
log_info "Resolved community VERSION=${VERSION} BASE_VERSION=${BASE_VERSION} BUILD=${BUILD}"

for value in \
    "VERSION=${VERSION}" \
    "BASE_VERSION=${BASE_VERSION}" \
    "BUILD=${BUILD}" \
    "IS_PRERELEASE=${IS_PRERELEASE}"; do
    printf '%s\n' "$value"
done

if [ -n "${GITHUB_ENV:-}" ]; then
    {
        printf 'VERSION=%s\n' "$VERSION"
        printf 'BASE_VERSION=%s\n' "$BASE_VERSION"
        printf 'BUILD=%s\n' "$BUILD"
        printf 'IS_PRERELEASE=%s\n' "$IS_PRERELEASE"
    } >> "$GITHUB_ENV"
fi
