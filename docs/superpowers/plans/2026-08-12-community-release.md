# Community Release Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Publish `v1.8.5-community.1` as a credential-free GitHub prerelease containing an ad-hoc-signed ZIP and drag-install DMG, dSYMs, checksums, and neutral first-launch instructions.

**Architecture:** A new `community-release.yml` workflow is isolated from the existing formal release workflow. Focused community scripts resolve and validate the community version, build and ad-hoc-sign the app, compose community release notes, generate checksums, and upload artifacts; the existing `package.sh` remains the shared ZIP/DMG/dSYMs packager.

**Tech Stack:** GitHub Actions, Bash 3.2, Xcode 26.4, `xcodebuild`, `codesign`, `ditto`, `hdiutil`, `shasum`, and GitHub CLI.

## Global Constraints

- The first community tag is exactly `v1.8.5-community.1` and is always a GitHub prerelease.
- The app's `CFBundleShortVersionString` remains `1.8.5`; the community suffix applies only to the tag and artifact names.
- The formal `.github/workflows/release.yml` path remains unchanged.
- The community workflow uses no Apple Developer ID, App Store Connect, Sparkle private key, or Pages mirror secrets.
- The DMG contains `LyricsX.app` and an `Applications` symlink to `/Applications`.
- Release copy contains neutral first-launch instructions and makes no claim that Apple signed, verified, or approved the release.
- Do not stage or commit the pre-existing changes to `LyricsX.xcodeproj/project.pbxproj`, either storyboard, or `LyricsX/Supporting Files/Info.plist`.

---

### Task 1: Resolve And Validate Community Versions

**Files:**
- Create: `Scripts/release/community-resolve-version.sh`
- Create: `Scripts/release/tests/community-resolve-version-test.sh`

**Interfaces:**
- Consumes: `INPUT_VERSION`, `GITHUB_ENV`, `LyricsX/Supporting Files/Info.plist`, and `ReleaseNotes/<base>_{en,zh}.md`.
- Produces: `VERSION=<base>-community.<positive integer>`, `BASE_VERSION=<base>`, `BUILD=<positive integer>`, and `IS_PRERELEASE=true` in stdout and `$GITHUB_ENV`.

- [ ] **Step 1: Write the failing runtime test**

Create a temporary fixture repository containing a minimal Info.plist with
`CFBundleShortVersionString=1.8.5`, `CFBundleVersion=2924`, and matching
`ReleaseNotes/1.8.5_en.md` and `_zh.md`. Source the future resolver with
`INPUT_VERSION=1.8.5-community.1` and assert all four outputs. Add failure
cases for `1.8.5`, `1.8.5-community.0`, base-version mismatch, and missing
base release notes.

- [ ] **Step 2: Run the test and confirm RED**

Run:

```bash
bash Scripts/release/tests/community-resolve-version-test.sh
```

Expected: non-zero because `community-resolve-version.sh` does not exist.

- [ ] **Step 3: Implement the resolver**

Use the exact regex:

```bash
^([0-9]+\.[0-9]+\.[0-9]+)-community\.([1-9][0-9]*)$
```

Set `VERSION` from `INPUT_VERSION`, extract `BASE_VERSION` from
`BASH_REMATCH[1]`, require the Info.plist version to equal `BASE_VERSION`,
require a positive-integer bundle build, require both base release-note files,
and fail if `gh release view "v${VERSION}"` succeeds when `gh` is available.
Always set `IS_PRERELEASE=true`.

- [ ] **Step 4: Verify GREEN and syntax**

Run:

```bash
bash Scripts/release/tests/community-resolve-version-test.sh
bash -n Scripts/release/community-resolve-version.sh
```

Expected: both exit 0.

- [ ] **Step 5: Commit Task 1**

```bash
git add Scripts/release/community-resolve-version.sh Scripts/release/tests/community-resolve-version-test.sh
git commit -m "build: validate community release versions"
```

### Task 2: Build And Verify An Ad-Hoc-Signed App

**Files:**
- Create: `Scripts/release/community-build.sh`
- Create: `Scripts/release/tests/community-build-test.sh`

**Interfaces:**
- Consumes: the `LyricsX` Xcode project and scheme.
- Produces: `build/Export/LyricsX.app` and a non-empty `build/LyricsX.xcarchive/dSYMs` directory compatible with `package.sh`.

- [ ] **Step 1: Write the failing runtime test**

Run `community-build.sh` in a temporary fixture with shell functions replacing
`xcodebuild`, `ditto`, and `codesign`. Capture arguments and assert:

```text
CODE_SIGNING_ALLOWED=NO
CODE_SIGNING_REQUIRED=NO
CODE_SIGN_IDENTITY=
```

Assert the script copies the built `.app` into `build/Export`, copies all
`.dSYM` bundles into `build/LyricsX.xcarchive/dSYMs`, calls
`codesign --force --deep --sign - --timestamp=none`, and then calls
`codesign --verify --deep --strict --verbose=2` on the exported app.

- [ ] **Step 2: Run the test and confirm RED**

Run:

```bash
bash Scripts/release/tests/community-build-test.sh
```

Expected: non-zero because `community-build.sh` does not exist.

- [ ] **Step 3: Implement the community build**

Use a dedicated DerivedData directory at `build/DerivedData-community` and:

```bash
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
```

Locate the unique `Build/Products/Release/LyricsX.app`, copy it with `ditto`,
collect every `.dSYM`, require the dSYMs directory to be non-empty, ad-hoc sign
the copied app, and verify its signature before returning success.

- [ ] **Step 4: Verify GREEN and syntax**

Run:

```bash
bash Scripts/release/tests/community-build-test.sh
bash -n Scripts/release/community-build.sh
```

Expected: both exit 0.

- [ ] **Step 5: Commit Task 2**

```bash
git add Scripts/release/community-build.sh Scripts/release/tests/community-build-test.sh
git commit -m "build: add credential-free macOS build"
```

### Task 3: Compose Community Notes, Checksums, And Release Upload

**Files:**
- Create: `Scripts/release/community-compose-notes.sh`
- Create: `Scripts/release/community-create-release.sh`
- Create: `Scripts/release/tests/community-publish-test.sh`

**Interfaces:**
- Consumes: `VERSION`, `BASE_VERSION`, `BUILD`, `ReleaseNotes/<base>_{en,zh}.md`, ZIP, DMG, and dSYMs ZIP.
- Produces: `build/body.md`, `build/LyricsX_<VERSION>+<BUILD>.sha256`, and GitHub prerelease `v<VERSION>` with all four artifacts.

- [ ] **Step 1: Write the failing publish-contract test**

Create fixture release notes and artifacts, stub `gh`, run both future scripts,
and assert:

- the body includes both base release notes;
- it includes “Open the DMG and drag LyricsX to Applications” and the Chinese
  equivalent;
- it includes the Privacy & Security / Open Anyway instruction in both
  languages;
- it contains none of `notarized`, `notarization`, `Apple verified`,
  `Apple approved`, or the maintainer-omitted Chinese phrase;
- the `.sha256` file contains exactly the ZIP and DMG basenames;
- the `gh release create` call includes `--prerelease`, ZIP, DMG, dSYMs ZIP,
  checksum file, and `--notes-file build/body.md`.

- [ ] **Step 2: Run the test and confirm RED**

Run:

```bash
bash Scripts/release/tests/community-publish-test.sh
```

Expected: non-zero because the community publish scripts do not exist.

- [ ] **Step 3: Implement community notes and publish scripts**

`community-compose-notes.sh` concatenates base English notes, English install
steps, a separator, base Chinese notes, and Chinese install steps into
`build/body.md`.

`community-create-release.sh` validates all inputs, runs:

```bash
shasum -a 256 "$APP_ZIP" "$APP_DMG" > "$CHECKSUM"
hdiutil verify "$APP_DMG"
```

It then invokes `gh release create "v${VERSION}" --prerelease`, targets
`GITHUB_SHA` when present, and uploads the four artifacts.

- [ ] **Step 4: Verify GREEN and syntax**

Run:

```bash
bash Scripts/release/tests/community-publish-test.sh
bash -n Scripts/release/community-compose-notes.sh Scripts/release/community-create-release.sh
```

Expected: both exit 0.

- [ ] **Step 5: Commit Task 3**

```bash
git add Scripts/release/community-compose-notes.sh Scripts/release/community-create-release.sh Scripts/release/tests/community-publish-test.sh
git commit -m "build: publish community release artifacts"
```

### Task 4: Add The Isolated Community Release Workflow

**Files:**
- Create: `.github/workflows/community-release.yml`
- Create: `Scripts/release/tests/community-workflow-test.sh`
- Verify: `Scripts/release/tests/package-dmg-layout-test.sh`

**Interfaces:**
- Consumes: manual `version` input and automatic `GITHUB_TOKEN`.
- Produces: the complete community prerelease through Tasks 1-3 and existing `package.sh`.

- [ ] **Step 1: Write the failing workflow-contract test**

Assert the future workflow:

- uses `workflow_dispatch` with required string input `version`;
- runs `community-resolve-version.sh`, `community-build.sh`, existing
  `package.sh`, `community-compose-notes.sh`, and
  `community-create-release.sh` in that order;
- grants `contents: write`;
- passes only `secrets.GITHUB_TOKEN`;
- contains none of `APPLE_`, `KEYCHAIN_`, `SPARKLE_`, `PAGES_MIRROR_`,
  `notarize.sh`, `sign-sparkle.sh`, or `publish-appcast.sh`.

- [ ] **Step 2: Run the test and confirm RED**

Run:

```bash
bash Scripts/release/tests/community-workflow-test.sh
```

Expected: non-zero because the workflow does not exist.

- [ ] **Step 3: Implement the workflow**

Create a `macos-26` job with `DEVELOPER_DIR` set to
`/Applications/Xcode_26.4.app/Contents/Developer`, checkout with
`fetch-depth: 0`, execute the five scripts in order, and upload the complete
`build/` directory as a seven-day diagnostic artifact when any step fails.

- [ ] **Step 4: Run all community and existing release checks**

Run:

```bash
bash Scripts/release/tests/community-workflow-test.sh
bash Scripts/release/tests/community-resolve-version-test.sh
bash Scripts/release/tests/community-build-test.sh
bash Scripts/release/tests/community-publish-test.sh
bash Scripts/release/tests/package-dmg-layout-test.sh
bash Scripts/release/tests/build-empty-auth-args-test.sh
bash -n Scripts/release/*.sh Scripts/release/tests/*.sh
```

Parse `.github/workflows/community-release.yml` with Ruby's `YAML.safe_load`
using aliases enabled and assert the result is a mapping. Expected: every
command exits 0.

- [ ] **Step 5: Commit Task 4**

```bash
git add .github/workflows/community-release.yml Scripts/release/tests/community-workflow-test.sh
git commit -m "ci: add community macOS release workflow"
```

### Task 5: Publish And Verify `v1.8.5-community.1`

**Files:**
- No source changes expected.

**Interfaces:**
- Consumes: the pushed community workflow on `main`.
- Produces: GitHub prerelease `v1.8.5-community.1` and downloadable artifacts.

- [ ] **Step 1: Review the complete implementation**

Review the range from `ab111ed` through the final workflow commit for spec
compliance, release-copy constraints, credential isolation, shell safety, and
artifact completeness. Resolve every critical or important finding before
publishing.

- [ ] **Step 2: Run fresh local verification**

Repeat every command from Task 4 Step 4 and require all exit codes to be 0.

- [ ] **Step 3: Push `main` without including user changes**

```bash
git push origin main
```

- [ ] **Step 4: Trigger and monitor the workflow**

Trigger **Community Release** on `main` with
`version=1.8.5-community.1`. Monitor the run until it succeeds or produces a
specific actionable failure; do not stop while the run is active.

- [ ] **Step 5: Verify the published prerelease**

Confirm the release tag, prerelease flag, neutral notes, and all four asset
names. Download the DMG to a temporary directory, run `hdiutil verify`, attach
it read-only, and assert the mounted root contains `LyricsX.app` and an
`Applications` symlink whose target is `/Applications`. Confirm the checksum
file validates the downloaded DMG and ZIP.
