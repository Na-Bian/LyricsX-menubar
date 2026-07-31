# Release DMG Installer Design

## Goal

Publish a GitHub Release DMG that opens with `LyricsX.app` and an `Applications`
shortcut so macOS users can drag the app to install it, while retaining the
existing ZIP-based Sparkle update feed.

## Approach

`Scripts/release/package.sh` will assemble a temporary DMG source directory
containing the notarized `LyricsX.app` and an `Applications` symbolic link to
`/Applications`. It will create `build/LyricsX_<VERSION>+<BUILD>.dmg` with
`hdiutil` and keep producing the existing app ZIP and dSYMs ZIP.

`Scripts/release/create-release.sh` will validate that DMG and attach it to
the GitHub Release. Sparkle signing and appcast publication continue to use
only the existing app ZIP.

## Constraints

- The DMG is created only after the existing notarization step.
- The release artifact name is `LyricsX_<VERSION>+<BUILD>.dmg`.
- The installer volume is named `LyricsX`.
- Existing user changes outside the release scripts and their test are not
  staged or committed.

## Verification

A shell regression test asserts the package script stages the app and
`Applications` link and that the release script requires/uploads the DMG.
Shell syntax validation covers both changed scripts. A local package run then
verifies the resulting DMG with `hdiutil verify` when local build artifacts
are available.
