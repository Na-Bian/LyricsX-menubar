# Community Release Design

## Goal

Add an independent GitHub Actions workflow that publishes LyricsX for macOS
without requiring Apple Developer Program credentials. The first release is a
prerelease tagged `v1.8.5-community.1`.

## Scope

The community workflow will:

- build the existing LyricsX macOS app without Developer ID credentials;
- apply ad-hoc signing to the complete app bundle after the build;
- produce a ZIP, dSYMs ZIP, and drag-install DMG;
- place `LyricsX.app` and an `Applications` symlink inside the DMG;
- produce a SHA-256 checksum file covering the downloadable ZIP and DMG;
- create a GitHub prerelease using the repository-provided `GITHUB_TOKEN`;
- include neutral first-launch instructions for macOS users.

The community workflow will not:

- import Developer ID certificates;
- call Apple's notarization service;
- sign or publish a Sparkle appcast;
- update the legacy Pages mirror;
- state that Apple signed, verified, or approved the release;
- use the phrase requested by the maintainer to be omitted from release copy.

## Architecture

The existing `.github/workflows/release.yml` remains the formal release path
and is not weakened. A new `.github/workflows/community-release.yml` owns the
credential-free flow.

A focused community build script will archive or build the Release app with
code signing disabled, copy it into `build/Export/LyricsX.app`, and apply
recursive ad-hoc signing. The existing DMG packaging behavior is reused so the
installer includes the app and the `/Applications` shortcut. Community
release notes are composed separately so the formal release notes remain
unchanged.

## Versioning And Artifacts

The workflow accepts a version in the form `1.8.5-community.1` and creates the
tag `v1.8.5-community.1`. It is always published as a GitHub prerelease.

Artifacts use the existing version/build naming convention:

- `LyricsX_1.8.5-community.1+<build>.dmg`
- `LyricsX_1.8.5-community.1+<build>.zip`
- `LyricsX_1.8.5-community.1+<build>.dSYMs.zip`
- `LyricsX_1.8.5-community.1+<build>.sha256`

The checksum file contains SHA-256 entries for the DMG and app ZIP. dSYMs are
uploaded for diagnostics but are not part of the end-user checksum list.

## Release Copy

The release body starts with the existing English and Chinese `1.8.5` release
notes, followed by concise installation instructions in both languages:

1. Open the DMG and drag LyricsX to Applications.
2. If macOS blocks the first launch, open System Settings, choose Privacy &
   Security, and select Open Anyway for LyricsX.

The copy is factual and neutral. It does not make claims about Apple
validation or approval.

## Failure Handling

The workflow stops before creating a GitHub Release if any build, ad-hoc
signing, package, checksum, or artifact validation step fails. It verifies the
DMG with `hdiutil verify`, checks the app's ad-hoc signature with
`codesign --verify`, and requires every expected artifact before invoking
`gh release create`.

## Verification

Shell regression tests will cover:

- the community workflow excludes Apple, Sparkle, and legacy mirror secrets;
- the build path disables normal signing and verifies the ad-hoc signature;
- the release is forced to prerelease status;
- release copy contains the two installation steps and omits prohibited
  claims;
- checksum generation and release upload include the expected files;
- the DMG packaging contract still includes `LyricsX.app` and the
  `Applications` symlink.

All changed shell scripts will pass `bash -n`, and workflow YAML will be parsed
with an available structured YAML parser before publishing.
