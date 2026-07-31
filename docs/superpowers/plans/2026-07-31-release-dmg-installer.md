# Release DMG Installer Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add a drag-to-Applications DMG to formal GitHub Releases without changing the Sparkle ZIP update path.

**Architecture:** The package script stages `LyricsX.app` and an `/Applications` symlink in a disposable directory, then produces a compressed UDZO DMG. The release script validates and uploads the DMG alongside the existing ZIP assets.

**Tech Stack:** Bash, macOS `hdiutil`, GitHub CLI.

## Global Constraints

- Keep `build/LyricsX_<VERSION>+<BUILD>.zip` as the Sparkle/appcast payload.
- Create `build/LyricsX_<VERSION>+<BUILD>.dmg` only from the notarized exported app.
- The DMG volume name is exactly `LyricsX` and includes `LyricsX.app` plus an `Applications` symlink to `/Applications`.
- Do not stage or commit pre-existing changes to `LyricsX.xcodeproj/project.pbxproj`, either storyboard, or `LyricsX/Supporting Files/Info.plist`.

---

### Task 1: Package and publish a drag-install DMG

**Files:**
- Create: `Scripts/release/tests/package-dmg-layout-test.sh`
- Modify: `Scripts/release/package.sh`
- Modify: `Scripts/release/create-release.sh`

**Interfaces:**
- Consumes: `VERSION`, `BUILD`, `build/Export/LyricsX.app`, and `build/LyricsX.xcarchive/dSYMs`.
- Produces: `build/LyricsX_<VERSION>+<BUILD>.dmg` and a GitHub Release attachment with that exact path.

- [ ] **Step 1: Write the failing regression test**

Create `Scripts/release/tests/package-dmg-layout-test.sh` that reads both
release scripts and fails unless `package.sh` declares `APP_DMG`, creates a
temporary staging directory, stages an `Applications` symlink to
`/Applications`, calls `hdiutil create` with `-volname "LyricsX"` and
`-srcfolder` set to that directory, and unless `create-release.sh` validates
and passes `APP_DMG` to `gh release create`.

- [ ] **Step 2: Run the regression test to verify it fails**

Run: `bash Scripts/release/tests/package-dmg-layout-test.sh`

Expected: non-zero exit because the existing scripts do not define or upload
the DMG.

- [ ] **Step 3: Implement DMG packaging and release upload**

In `package.sh`, define:

```bash
APP_DMG="build/LyricsX_${VERSION}+${BUILD}.dmg"
DMG_STAGE_DIR="$(mktemp -d "${TMPDIR:-/tmp}/LyricsX.dmg.XXXXXX")"
```

Use a `trap` to remove `DMG_STAGE_DIR`; copy the app using `ditto`, link
`/Applications` as `"$DMG_STAGE_DIR/Applications"`, remove an old DMG, and
create the new one with:

```bash
hdiutil create -volname "LyricsX" -srcfolder "$DMG_STAGE_DIR" -ov -format UDZO "$APP_DMG"
```

Add the DMG to the final artifact listing. In `create-release.sh`, define and
require `APP_DMG`, and append it to `gh release create` after the app ZIP.

- [ ] **Step 4: Run regression and syntax verification**

Run:

```bash
bash Scripts/release/tests/package-dmg-layout-test.sh
bash -n Scripts/release/package.sh Scripts/release/create-release.sh
```

Expected: both commands exit 0.

- [ ] **Step 5: Run a local package verification when artifacts exist**

Run `VERSION=1.8.5 BUILD=2924 bash Scripts/release/package.sh` and then
`hdiutil verify build/LyricsX_1.8.5+2924.dmg`. If the app or dSYMs artifacts
are absent, report that formal verification will be performed by the signing
GitHub Actions workflow.

- [ ] **Step 6: Commit only task files**

```bash
git add Scripts/release/package.sh Scripts/release/create-release.sh Scripts/release/tests/package-dmg-layout-test.sh docs/superpowers/specs/2026-07-31-release-dmg-installer-design.md docs/superpowers/plans/2026-07-31-release-dmg-installer.md
git commit -m "build: add drag-install DMG release artifact"
```
