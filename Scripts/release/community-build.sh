#!/usr/bin/env bash
# Build LyricsX without Developer ID credentials and apply an ad-hoc signature.

set -euo pipefail

HERE="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "${HERE}/lib.sh"
cd "$(repo_root)"

DERIVED_DATA="build/DerivedData-community"
EXPORT_PATH="build/Export"
DSYMS_DIR="build/LyricsX.xcarchive/dSYMs"
APP_PATH="${EXPORT_PATH}/LyricsX.app"

rm -rf "$DERIVED_DATA" "$EXPORT_PATH" "build/LyricsX.xcarchive"
mkdir -p build "$EXPORT_PATH" "$DSYMS_DIR"

export LYRICSX_SKIP_BUILD_BUMP=1
export LYRICSX_USE_LOCAL_DEPENDENCY=0

log_info "Building LyricsX without distribution credentials"
xcodebuild \
    -project LyricsX.xcodeproj \
    -scheme LyricsX \
    -configuration Release \
    -destination 'generic/platform=macOS' \
    -derivedDataPath "$DERIVED_DATA" \
    -skipMacroValidation \
    -skipPackagePluginValidation \
    CODE_SIGNING_ALLOWED=NO \
    CODE_SIGNING_REQUIRED=NO \
    CODE_SIGN_IDENTITY= \
    build

BUILT_APP="${DERIVED_DATA}/Build/Products/Release/LyricsX.app"
[ -d "$BUILT_APP" ] || die "Expected exactly ${BUILT_APP}"

log_info "Copying app to ${APP_PATH}"
ditto "$BUILT_APP" "$APP_PATH"

DSYM_COUNT=0
while IFS= read -r dsym; do
    [ -n "$dsym" ] || continue
    ditto "$dsym" "${DSYMS_DIR}/$(basename "$dsym")"
    DSYM_COUNT=$((DSYM_COUNT + 1))
done < <(find "${DERIVED_DATA}/Build/Products/Release" -maxdepth 1 -type d -name '*.dSYM' -print | sort)

[ "$DSYM_COUNT" -gt 0 ] || die "No dSYMs found under ${DERIVED_DATA}/Build/Products/Release"

log_info "Applying ad-hoc signature"
codesign --force --deep --sign - --timestamp=none "$APP_PATH"
codesign --verify --deep --strict --verbose=2 "$APP_PATH"

log_info "Built and verified ${APP_PATH} with ${DSYM_COUNT} dSYM bundle(s)"
