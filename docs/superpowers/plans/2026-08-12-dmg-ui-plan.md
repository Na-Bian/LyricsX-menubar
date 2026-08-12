# LyricsX DMG Installer UI Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make the LyricsX DMG look like a JetBrains-style drag-install canvas while preserving the existing app, Applications symlink, artifacts, and release flow.

**Architecture:** Keep `Scripts/release/package.sh` as the packaging entry point. Add a focused `community-dmg-layout.sh` helper that stages a static background and applies Finder metadata to a mounted read-write image when Finder scripting is available; any visual configuration failure is non-fatal. Package through a temporary UDRW image and convert it to UDZO only after the layout attempt, then extend the existing DMG contract test and retain real hdiutil mount verification.

**Tech Stack:** Bash 3.2, macOS `sips`, `osascript`, `hdiutil`, `ditto`, Finder `.DS_Store` metadata, 2× Retina PNG assets.

## Global Constraints

- DMG root contains `LyricsX.app` and an `Applications` symlink targeting `/Applications`.
- Visual direction is JetBrains-inspired spatial layout with LyricsX branding; do not copy JetBrains logos or copy.
- The app code, Bundle ID, version, formal release workflow, and user-owned unstaged files remain unchanged.
- ZIP, dSYMs ZIP, checksum generation, release upload, and ad-hoc signing behavior remain unchanged.
- Missing optional Finder configuration tools must not fail DMG creation.
- All changed shell scripts pass `bash -n`; tests run under macOS Bash 3.2-compatible syntax.

---

### Task 1: Add the DMG background resource and layout helper

**Files:**
- Create: `Scripts/release/dmg-assets/LyricsX-dmg-background.svg`
- Create: `Scripts/release/dmg-assets/LyricsX-dmg-background.png`
- Create: `Scripts/release/community-dmg-layout.sh`
- Test: `Scripts/release/tests/community-dmg-layout-test.sh`

**Interfaces:**
- Consumes: `prepare <DMG_STAGE_DIR>` or `apply <DMG_MOUNT_DIR>`, where the directory contains `LyricsX.app` and `Applications`.
- Produces: `.background/LyricsX-dmg-background.png`, optional mounted-volume `.DS_Store` Finder metadata, and a successful exit when metadata tooling is unavailable.

- [ ] **Step 1: Write the failing layout contract test**

The test copies the future helper into a fixture, creates a fake app and Applications symlink, stubs `osascript` to fail, runs the helper, and asserts the background resource is copied, the symlink remains intact, and the helper exits successfully. It also asserts the script declares the JetBrains-inspired positions and the `drag to` copy through runtime-generated metadata inputs rather than requiring a specific GUI tool.

- [ ] **Step 2: Run the test and confirm RED**

Run:

```bash
bash Scripts/release/tests/community-dmg-layout-test.sh
```

Expected: non-zero because `community-dmg-layout.sh` and the background resource do not exist.

- [ ] **Step 3: Create a static background**

Create a deterministic 640×400 logical SVG with a white/light-gray canvas, a restrained LyricsX blue-purple gradient accent, exact `LYRICS FOR YOUR MENUBAR` and `drag to` text, and reserved empty areas for the Finder icons. Render it natively to a 1280×800 PNG with 144 DPI metadata. Do not include JetBrains artwork or copied wording; the actual app and Applications labels come from Finder.

- [ ] **Step 4: Implement the helper**

Use Bash 3.2-safe subcommands. `prepare` stages the PNG:

```bash
DMG_STAGE_DIR="$2"
BACKGROUND_DIR="${DMG_STAGE_DIR}/.background"
mkdir -p "$BACKGROUND_DIR"
ditto "${HERE}/dmg-assets/LyricsX-dmg-background.png" "$BACKGROUND_DIR/LyricsX-dmg-background.png"
```

`apply` receives the mounted read-write volume. If `/usr/bin/osascript` is available, identify the Finder disk by the basename of the custom mount point, then use explicit `targetDisk`, `targetWindow`, and `viewOptions` references to set window bounds, icon positions, icon size, and background image. If the command fails, log a warning and return success. The canvas is 640×400 points backed by a 1280×800 Retina PNG, with `LyricsX.app` at `(165, 220)` and `Applications` at `(475, 220)`.

- [ ] **Step 5: Run the test and syntax check**

Run:

```bash
bash Scripts/release/tests/community-dmg-layout-test.sh
bash -n Scripts/release/community-dmg-layout.sh
```

Expected: both exit 0.

- [ ] **Step 6: Commit Task 1**

```bash
git add Scripts/release/community-dmg-layout.sh Scripts/release/dmg-assets/LyricsX-dmg-background.svg Scripts/release/dmg-assets/LyricsX-dmg-background.png Scripts/release/tests/community-dmg-layout-test.sh
git commit -m "build: add LyricsX DMG installer layout"
```

### Task 2: Wire the layout helper into DMG packaging

**Files:**
- Modify: `Scripts/release/package.sh`
- Modify: `Scripts/release/tests/package-dmg-layout-test.sh`

**Interfaces:**
- Consumes: the helper from Task 1 and the existing app/dSYM staging paths.
- Produces: a temporary UDRW image, applies layout after mounting it read-write, then converts it to the final UDZO DMG.

- [ ] **Step 1: Extend the existing contract test**

Assert `package.sh` invokes `community-dmg-layout.sh prepare` after staging the app and symlink, creates and attaches a temporary UDRW image, invokes `community-dmg-layout.sh apply` on the mount point, detaches it, and converts it to UDZO.

- [ ] **Step 2: Run the test and confirm RED**

Run:

```bash
bash Scripts/release/tests/package-dmg-layout-test.sh
```

Expected: non-zero because package.sh does not yet invoke the helper.

- [ ] **Step 3: Wire the helper**

Add the standard writable-image flow:

```bash
bash "${HERE}/community-dmg-layout.sh" prepare "$DMG_STAGE_DIR"
hdiutil create -volname "LyricsX" -srcfolder "$DMG_STAGE_DIR" -ov -format UDRW "$DMG_RW"
hdiutil attach -readwrite -noverify -noautoopen -mountpoint "$DMG_MOUNT_DIR" "$DMG_RW"
bash "${HERE}/community-dmg-layout.sh" apply "$DMG_MOUNT_DIR"
hdiutil detach "$DMG_MOUNT_DIR"
hdiutil convert "$DMG_RW" -format UDZO -o "$APP_DMG"
```

Use a cleanup trap that detaches the volume on failure and removes both temporary directories and the temporary read-write image. Keep existing ZIP/DMG/dSYMs final names unchanged.

- [ ] **Step 4: Run the contract and syntax checks**

Run:

```bash
bash Scripts/release/tests/package-dmg-layout-test.sh
bash -n Scripts/release/package.sh Scripts/release/community-dmg-layout.sh
```

Expected: both exit 0.

- [ ] **Step 5: Commit Task 2**

```bash
git add Scripts/release/package.sh Scripts/release/tests/package-dmg-layout-test.sh
git commit -m "build: wire DMG installer layout into packaging"
```

### Task 3: Build and inspect a real DMG

**Files:**
- No source changes expected unless verification exposes a defect.

- [ ] **Step 1: Run the complete shell regression suite**

Run:

```bash
bash Scripts/release/tests/community-dmg-layout-test.sh
bash Scripts/release/tests/package-dmg-layout-test.sh
bash Scripts/release/tests/community-workflow-test.sh
bash Scripts/release/tests/community-resolve-version-test.sh
bash Scripts/release/tests/community-build-test.sh
bash Scripts/release/tests/community-publish-test.sh
bash Scripts/release/tests/build-empty-auth-args-test.sh
bash -n Scripts/release/*.sh Scripts/release/tests/*.sh
```

- [ ] **Step 2: Package a real DMG from existing build artifacts**

If `build/Export/LyricsX.app` and `build/LyricsX.xcarchive/dSYMs` are present, run:

```bash
VERSION=1.8.5-community.1 BUILD=2924 bash Scripts/release/package.sh
```

Otherwise use the existing community workflow/build path to regenerate them before packaging.

- [ ] **Step 3: Verify the DMG**

Run `hdiutil verify`, attach read-only, and assert the mounted root contains `LyricsX.app` and an `Applications` symlink to `/Applications`. Confirm the `.background` resource is present in the staging path or Finder metadata exists in the mounted volume when supported by the runner.

- [ ] **Step 4: Commit any verification-only fix and report artifact path**

If no fixes are needed, report the generated DMG path and verification output.

### Task 4: Replace the existing community prerelease

**Files:**
- No source changes expected.

- [ ] **Step 1: Push the verified commits to main**

Push only committed DMG UI changes; the four user-owned unstaged project files remain local and unstaged.

- [ ] **Step 2: Remove the existing release and tag**

After confirming the current tag is exactly `v1.8.5-community.1`, run:

```bash
gh release delete v1.8.5-community.1 --repo Na-Bian/LyricsX-menubar --cleanup-tag --yes
```

- [ ] **Step 3: Re-run Community Release**

Trigger `.github/workflows/community-release.yml` on `main` with
`version=1.8.5-community.1` and monitor it to a terminal result.

- [ ] **Step 4: Verify the replacement release**

Confirm the prerelease tag targets the new DMG UI commit, all four assets are present,
checksums validate, and the downloaded DMG contains the branded background,
`LyricsX.app`, and the `/Applications` symlink.
