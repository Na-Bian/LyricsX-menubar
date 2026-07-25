# macOS Menu Bar Lyrics Visibility and Transition Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Make macOS menu bar lyrics collapse to the LyricsX icon after 1.5 seconds without playback, restore immediately when playback resumes, and crossfade lyric changes over 180 milliseconds.

**Architecture:** Keep `MenuBarLyricsEnabled` as the persistent user preference and derive a separate runtime visibility action from the current playback state. Put the pure decision and transition state in `LyricsXFoundation`, render text through a focused two-label `CrossfadeMarqueeView`, and let `MenuBarLyricsController` own the cancellable delay and `NSStatusItem` lifecycle.

**Tech Stack:** Swift 6.2, Swift Testing, Combine, AppKit, `NSAnimationContext`, existing `MarqueeLabel`, Xcode project + local Swift Package.

## Global Constraints

- Scope is the macOS menu bar lyrics only; do not modify desktop lyrics, the lyrics window, Touch Bar lyrics, or the Windows migration.
- Preserve `MenuBarLyricsEnabled` as user intent; runtime playback changes must never write this preference.
- Hide only after playback remains inactive for exactly 1.5 seconds; resume playback immediately.
- Use a 0.18-second crossfade and skip it when macOS Reduce Motion is enabled.
- Keep the existing LyricsX icon and menu available whenever lyrics are collapsed.
- Do not add user-facing settings, persistence keys, dependencies, or localization strings.
- Preserve the existing uncommitted edits in `LyricsX.xcodeproj/project.pbxproj`, `LyricsX/Base.lproj/Main.storyboard`, and `LyricsX/Base.lproj/Preferences.storyboard`.

---

## File Structure

- Create `LyricsXPackage/Sources/LyricsXFoundation/MenuBarLyricsVisibilityPolicy.swift`: pure visibility actions and 1.5-second policy.
- Create `LyricsXPackage/Sources/LyricsXFoundation/CrossfadeTextTransitionState.swift`: pure latest-text transition state used by the AppKit view.
- Create `LyricsXPackage/Tests/LyricsXFoundationTests/MenuBarLyricsVisibilityPolicyTests.swift`: visibility policy coverage.
- Create `LyricsXPackage/Tests/LyricsXFoundationTests/CrossfadeTextTransitionStateTests.swift`: initial, repeated, rapid, and completed transition coverage.
- Delete `LyricsXPackage/Tests/LyricsXFoundationTests/LyricsXPackageTests.swift`: remove the placeholder test after real tests exist.
- Create `LyricsX/View/CrossfadeMarqueeView.swift`: two-label AppKit crossfade renderer.
- Modify `LyricsX/Controller/MenuBarLyricsController.swift`: subscribe to playback state, schedule/cancel hiding, and use the new renderer.
- Modify `LyricsX.xcodeproj/project.pbxproj`: add `CrossfadeMarqueeView.swift` to the existing View group and LyricsX Sources phase without disturbing current user edits.

---

### Task 1: Pure Visibility and Text Transition State

**Files:**
- Create: `LyricsXPackage/Sources/LyricsXFoundation/MenuBarLyricsVisibilityPolicy.swift`
- Create: `LyricsXPackage/Sources/LyricsXFoundation/CrossfadeTextTransitionState.swift`
- Create: `LyricsXPackage/Tests/LyricsXFoundationTests/MenuBarLyricsVisibilityPolicyTests.swift`
- Create: `LyricsXPackage/Tests/LyricsXFoundationTests/CrossfadeTextTransitionStateTests.swift`
- Delete: `LyricsXPackage/Tests/LyricsXFoundationTests/LyricsXPackageTests.swift`

**Interfaces:**
- Produces: `MenuBarLyricsVisibilityPolicy.action(isEnabled:isPlaying:isShowingLyrics:) -> MenuBarLyricsVisibilityAction`
- Produces: `MenuBarLyricsVisibilityPolicy.actionAfterHideDelay(isEnabled:isPlaying:) -> MenuBarLyricsVisibilityAction`
- Produces: `CrossfadeTextTransitionState.update(to:animated:) -> CrossfadeTextUpdate`
- Produces: `CrossfadeTextTransitionState.completeTransition()`

- [ ] **Step 1: Write failing visibility tests**

```swift
import Testing
@testable import LyricsXFoundation

@Suite("Menu bar lyrics visibility policy")
struct MenuBarLyricsVisibilityPolicyTests {
    private let policy = MenuBarLyricsVisibilityPolicy()

    @Test func playingShowsLyricsWhenEnabled() {
        #expect(policy.action(
            isEnabled: true,
            isPlaying: true,
            isShowingLyrics: false
        ) == .showLyrics)
    }

    @Test func userDisabledAlwaysShowsIcon() {
        #expect(policy.action(
            isEnabled: false,
            isPlaying: true,
            isShowingLyrics: true
        ) == .showIcon)
    }

    @Test func pauseSchedulesHideOnlyWhenLyricsAreVisible() {
        #expect(policy.action(
            isEnabled: true,
            isPlaying: false,
            isShowingLyrics: true
        ) == .scheduleHide(after: 1.5))
        #expect(policy.action(
            isEnabled: true,
            isPlaying: false,
            isShowingLyrics: false
        ) == .showIcon)
    }

    @Test func delayedActionRechecksCurrentPlayback() {
        #expect(policy.actionAfterHideDelay(
            isEnabled: true,
            isPlaying: false
        ) == .showIcon)
        #expect(policy.actionAfterHideDelay(
            isEnabled: true,
            isPlaying: true
        ) == .showLyrics)
        #expect(policy.actionAfterHideDelay(
            isEnabled: false,
            isPlaying: true
        ) == .showIcon)
    }
}
```

- [ ] **Step 2: Write failing transition-state tests**

```swift
import Testing
@testable import LyricsXFoundation

@Suite("Crossfade text transition state")
struct CrossfadeTextTransitionStateTests {
    @Test func firstTextReplacesImmediately() {
        var state = CrossfadeTextTransitionState()

        #expect(state.update(to: "First", animated: true) == .replace)
        #expect(state.displayedText == "First")
        #expect(state.incomingText == nil)
    }

    @Test func nextTextBeginsCrossfade() {
        var state = CrossfadeTextTransitionState(displayedText: "First")

        #expect(state.update(to: "Second", animated: true) == .begin)
        #expect(state.displayedText == "First")
        #expect(state.incomingText == "Second")
    }

    @Test func rapidUpdateReplacesOnlyIncomingText() {
        var state = CrossfadeTextTransitionState(displayedText: "First")
        _ = state.update(to: "Second", animated: true)

        #expect(state.update(to: "Latest", animated: true) == .updateIncoming)
        #expect(state.displayedText == "First")
        #expect(state.incomingText == "Latest")

        state.completeTransition()
        #expect(state.displayedText == "Latest")
        #expect(state.incomingText == nil)
    }

    @Test func repeatedTextDoesNothing() {
        var state = CrossfadeTextTransitionState(displayedText: "Same")
        #expect(state.update(to: "Same", animated: true) == .noChange)
    }

    @Test func disabledAnimationReplacesImmediately() {
        var state = CrossfadeTextTransitionState(displayedText: "First")
        #expect(state.update(to: "Second", animated: false) == .replace)
        #expect(state.displayedText == "Second")
        #expect(state.incomingText == nil)
    }
}
```

- [ ] **Step 3: Run the tests to verify they fail**

Run:

```bash
swift test --package-path LyricsXPackage --filter MenuBarLyricsVisibilityPolicyTests
swift test --package-path LyricsXPackage --filter CrossfadeTextTransitionStateTests
```

Expected: both commands fail because the policy and transition types do not exist.

- [ ] **Step 4: Implement the visibility policy**

```swift
import Foundation

public enum MenuBarLyricsVisibilityAction: Equatable, Sendable {
    case showIcon
    case showLyrics
    case scheduleHide(after: TimeInterval)
}

public struct MenuBarLyricsVisibilityPolicy: Sendable {
    public static let defaultPauseHideDelay: TimeInterval = 1.5

    public let pauseHideDelay: TimeInterval

    public init(
        pauseHideDelay: TimeInterval = MenuBarLyricsVisibilityPolicy.defaultPauseHideDelay
    ) {
        self.pauseHideDelay = pauseHideDelay
    }

    public func action(
        isEnabled: Bool,
        isPlaying: Bool,
        isShowingLyrics: Bool
    ) -> MenuBarLyricsVisibilityAction {
        guard isEnabled else {
            return .showIcon
        }
        if isPlaying {
            return .showLyrics
        }
        return isShowingLyrics
            ? .scheduleHide(after: pauseHideDelay)
            : .showIcon
    }

    public func actionAfterHideDelay(
        isEnabled: Bool,
        isPlaying: Bool
    ) -> MenuBarLyricsVisibilityAction {
        isEnabled && isPlaying ? .showLyrics : .showIcon
    }
}
```

- [ ] **Step 5: Implement the transition state**

```swift
public enum CrossfadeTextUpdate: Equatable, Sendable {
    case noChange
    case replace
    case begin
    case updateIncoming
}

public struct CrossfadeTextTransitionState: Equatable, Sendable {
    public private(set) var displayedText: String?
    public private(set) var incomingText: String?

    public init(displayedText: String? = nil) {
        self.displayedText = displayedText
    }

    public mutating func update(
        to text: String,
        animated: Bool
    ) -> CrossfadeTextUpdate {
        if incomingText == text || incomingText == nil && displayedText == text {
            return .noChange
        }

        guard animated, displayedText != nil else {
            displayedText = text
            incomingText = nil
            return .replace
        }

        if incomingText != nil {
            incomingText = text
            return .updateIncoming
        }

        incomingText = text
        return .begin
    }

    public mutating func completeTransition() {
        guard let incomingText else {
            return
        }
        displayedText = incomingText
        self.incomingText = nil
    }
}
```

- [ ] **Step 6: Run all package tests**

Run:

```bash
swift test --package-path LyricsXPackage
```

Expected: all Swift Testing cases pass with zero failures.

- [ ] **Step 7: Commit the pure state**

```bash
git add LyricsXPackage/Sources/LyricsXFoundation/MenuBarLyricsVisibilityPolicy.swift \
  LyricsXPackage/Sources/LyricsXFoundation/CrossfadeTextTransitionState.swift \
  LyricsXPackage/Tests/LyricsXFoundationTests/MenuBarLyricsVisibilityPolicyTests.swift \
  LyricsXPackage/Tests/LyricsXFoundationTests/CrossfadeTextTransitionStateTests.swift \
  LyricsXPackage/Tests/LyricsXFoundationTests/LyricsXPackageTests.swift
git commit -m "feat: add menu bar lyric display state"
```

---

### Task 2: Crossfade Marquee View

**Files:**
- Create: `LyricsX/View/CrossfadeMarqueeView.swift`
- Modify: `LyricsX.xcodeproj/project.pbxproj`

**Interfaces:**
- Consumes: `CrossfadeTextTransitionState` and `CrossfadeTextUpdate` from Task 1.
- Produces: `CrossfadeMarqueeView.setStringValue(_:lineDisplayTime:animated:)`
- Produces: `CrossfadeMarqueeView.stopAnimations()`

- [ ] **Step 1: Add the source file to the Xcode project before it exists**

Add these exact objects:

```text
E9F710022F2B100100000001 /* CrossfadeMarqueeView.swift in Sources */ = {
    isa = PBXBuildFile;
    fileRef = E9F710012F2B100100000001 /* CrossfadeMarqueeView.swift */;
};

E9F710012F2B100100000001 /* CrossfadeMarqueeView.swift */ = {
    isa = PBXFileReference;
    lastKnownFileType = sourcecode.swift;
    path = CrossfadeMarqueeView.swift;
    sourceTree = "<group>";
};
```

Add `E9F710012F2B100100000001 /* CrossfadeMarqueeView.swift */` to the existing View group, and add `E9F710022F2B100100000001 /* CrossfadeMarqueeView.swift in Sources */` to the LyricsX `PBXSourcesBuildPhase`. Preserve every pre-existing project-file change.

- [ ] **Step 2: Run the app build to verify the missing source fails**

Run:

```bash
xcodebuild -project LyricsX.xcodeproj -scheme LyricsX -configuration Debug build
```

Expected: FAIL because `LyricsX/View/CrossfadeMarqueeView.swift` is referenced but does not exist.

- [ ] **Step 3: Create the two-label renderer**

```swift
import AppKit
import LyricsXFoundation
import MarqueeLabel

final class CrossfadeMarqueeView: NSView {
    private static let transitionDuration: TimeInterval = 0.18

    private let labels = [
        MarqueeLabel(frame: .zero),
        MarqueeLabel(frame: .zero),
    ]
    private var activeIndex = 0
    private var transitionGeneration: UInt = 0
    private var transitionState = CrossfadeTextTransitionState()

    override init(frame frameRect: NSRect) {
        super.init(frame: frameRect)
        wantsLayer = true
        for (index, label) in labels.enumerated() {
            label.alphaValue = index == activeIndex ? 1 : 0
            addSubview(label)
        }
    }

    @available(*, unavailable)
    required init?(coder: NSCoder) {
        fatalError("init(coder:) has not been implemented")
    }

    override func layout() {
        super.layout()
        labels.forEach { $0.frame = bounds }
    }

    func setStringValue(
        _ value: String,
        lineDisplayTime: TimeInterval,
        animated: Bool
    ) {
        let shouldAnimate = animated
            && !NSWorkspace.shared.accessibilityDisplayShouldReduceMotion
        let update = transitionState.update(to: value, animated: shouldAnimate)

        switch update {
        case .noChange:
            let contentLabel = transitionState.incomingText == nil
                ? currentContentLabel
                : incomingContentLabel
            contentLabel.setStringValue(
                value,
                lineDisplayTime: lineDisplayTime
            )
        case .replace:
            renderImmediately(value, lineDisplayTime: lineDisplayTime)
        case .begin:
            beginCrossfade(value, lineDisplayTime: lineDisplayTime)
        case .updateIncoming:
            incomingContentLabel.setStringValue(
                value,
                lineDisplayTime: lineDisplayTime
            )
        }
    }

    func stopAnimations() {
        transitionGeneration &+= 1
        labels.forEach { label in
            label.layer?.removeAllAnimations()
            label.alphaValue = 0
        }
        labels[activeIndex].alphaValue = 1
    }

    private var currentContentLabel: MarqueeLabel {
        labels[activeIndex]
    }

    private var incomingContentLabel: MarqueeLabel {
        labels[1 - activeIndex]
    }

    private func renderImmediately(
        _ value: String,
        lineDisplayTime: TimeInterval
    ) {
        transitionGeneration &+= 1
        labels.forEach { $0.layer?.removeAllAnimations() }
        currentContentLabel.setStringValue(
            value,
            lineDisplayTime: lineDisplayTime
        )
        currentContentLabel.alphaValue = 1
        incomingContentLabel.alphaValue = 0
    }

    private func beginCrossfade(
        _ value: String,
        lineDisplayTime: TimeInterval
    ) {
        transitionGeneration &+= 1
        let generation = transitionGeneration
        let outgoingLabel = currentContentLabel
        let incomingLabel = incomingContentLabel

        incomingLabel.setStringValue(
            value,
            lineDisplayTime: lineDisplayTime
        )
        incomingLabel.alphaValue = 0

        NSAnimationContext.runAnimationGroup { context in
            context.duration = Self.transitionDuration
            context.timingFunction = .swiftOut
            outgoingLabel.animator().alphaValue = 0
            incomingLabel.animator().alphaValue = 1
        } completionHandler: { [weak self] in
            guard let self, self.transitionGeneration == generation else {
                return
            }
            self.activeIndex = 1 - self.activeIndex
            self.transitionState.completeTransition()
            outgoingLabel.alphaValue = 0
            incomingLabel.alphaValue = 1
        }
    }
}
```

- [ ] **Step 4: Build after adding the view**

Run:

```bash
xcodebuild -project LyricsX.xcodeproj -scheme LyricsX -configuration Debug build
```

Expected: PASS with no missing-file or Swift compiler errors.

- [ ] **Step 5: Check the project diff preserves existing edits**

Run:

```bash
git diff -- LyricsX.xcodeproj/project.pbxproj
```

Expected: the existing deployment-target edit remains, plus only one file reference, one build-file entry, one View-group entry, and one Sources-phase entry for `CrossfadeMarqueeView.swift`.

- [ ] **Step 6: Commit the renderer**

```bash
git add LyricsX/View/CrossfadeMarqueeView.swift LyricsX.xcodeproj/project.pbxproj
git commit -m "feat: add menu bar lyric crossfade view"
```

---

### Task 3: Playback-Aware Menu Bar Controller

**Files:**
- Modify: `LyricsX/Controller/MenuBarLyricsController.swift`

**Interfaces:**
- Consumes: `MenuBarLyricsVisibilityPolicy` from Task 1.
- Consumes: `CrossfadeMarqueeView` from Task 2.
- Preserves: `statusBarMenu`, `screenLyrics`, lyric search retries, width clamping, icon menu behavior.

- [ ] **Step 1: Replace the single label with the crossfade view**

Remove `import MarqueeLabel` and replace the existing property with:

```swift
private lazy var marqueeView = CrossfadeMarqueeView(
    frame: .init(x: 0, y: 0, width: lyricItemLength, height: 22)
)
private let visibilityPolicy = MenuBarLyricsVisibilityPolicy()
private var pendingHideWorkItem: DispatchWorkItem?
```

Change `screenLyrics.didSet` to call a text-only refresh:

```swift
didSet {
    DispatchQueue.main.async { [weak self] in
        self?.refreshLyricsText(animated: true)
    }
}
```

- [ ] **Step 2: Subscribe separately to playback, preference, and layout changes**

Add:

```swift
selectedPlayer.playbackStateWillChange
    .receive(on: DispatchQueue.main)
    .sink { [weak self] state in
        self?.reconcileVisibility(isPlaying: state.isPlaying)
    }
    .store(in: &cancelBag)

defaults.publisher(for: [.menuBarLyricsEnabled])
    .prepend()
    .receive(on: DispatchQueue.main)
    .sink { [weak self] _ in
        self?.reconcileVisibility(
            isPlaying: selectedPlayer.playbackState.isPlaying
        )
    }
    .store(in: &cancelBag)

defaults.publisher(for: [.combinedMenubarLyrics, .menuBarLyricsWidth])
    .prepend()
    .receive(on: DispatchQueue.main)
    .sink { [weak self] _ in
        self?.refreshLyricStatusItemLayout()
    }
    .store(in: &cancelBag)
```

Remove the old combined defaults subscription that routed all three keys through `updateStatusItems()`.

- [ ] **Step 3: Implement visibility reconciliation and the cancellable delay**

```swift
private func reconcileVisibility(isPlaying: Bool) {
    precondition(Thread.isMainThread)
    let action = visibilityPolicy.action(
        isEnabled: defaults[.menuBarLyricsEnabled],
        isPlaying: isPlaying,
        isShowingLyrics: lyricStatusItem != nil
    )
    applyVisibilityAction(action)
}

private func applyVisibilityAction(
    _ action: MenuBarLyricsVisibilityAction
) {
    switch action {
    case .showIcon:
        cancelPendingHide()
        showIconStatusItem()
    case .showLyrics:
        cancelPendingHide()
        showLyricStatusItem()
    case .scheduleHide(let delay):
        scheduleHide(after: delay)
    }
}

private func scheduleHide(after delay: TimeInterval) {
    cancelPendingHide()
    let workItem = DispatchWorkItem { [weak self] in
        guard let self else {
            return
        }
        self.pendingHideWorkItem = nil
        self.applyVisibilityAction(
            self.visibilityPolicy.actionAfterHideDelay(
                isEnabled: defaults[.menuBarLyricsEnabled],
                isPlaying: selectedPlayer.playbackState.isPlaying
            )
        )
    }
    pendingHideWorkItem = workItem
    DispatchQueue.main.asyncAfter(
        deadline: .now() + delay,
        execute: workItem
    )
}

private func cancelPendingHide() {
    pendingHideWorkItem?.cancel()
    pendingHideWorkItem = nil
}
```

- [ ] **Step 4: Cancel delayed work if the controller is ever released**

```swift
deinit {
    pendingHideWorkItem?.cancel()
}
```

- [ ] **Step 5: Split icon, lyric, text, and layout updates**

Implement these responsibilities:

```swift
private func showIconStatusItem() {
    removeLyricStatusItem()
    if iconStatusItem == nil {
        setupIconStatusItem()
    }
}

private func showLyricStatusItem() {
    removeIconStatusItem()
    if lyricStatusItem == nil {
        setupLyricStatusItem()
        refreshLyricsText(animated: false)
    } else {
        refreshLyricStatusItemLayout()
    }
}

private func refreshLyricsText(animated: Bool) {
    guard lyricStatusItem != nil else {
        return
    }
    marqueeView.setStringValue(
        screenLyrics.lyrics,
        lineDisplayTime: screenLyrics.duration,
        animated: animated
    )
}

private func refreshLyricStatusItemLayout() {
    guard lyricStatusItem != nil else {
        return
    }
    let width = lyricItemLength
    lyricStatusItem?.length = width
    marqueeView.frame = .init(x: 0, y: 0, width: width, height: 22)
    lyricStatusItem?.button?.frame = marqueeView.bounds
}
```

Update `setupLyricStatusItem()` to add `marqueeView`, and update `removeLyricStatusItem()` to call `marqueeView.stopAnimations()` before removing it. Delete the old `updateStatusItems()` and `updateLyricItemWidth()` methods after all call sites are replaced.

- [ ] **Step 6: Preserve the current lyric during the 1.5-second grace period**

Remove this early branch from `handleLyricsDisplay(event:)`:

```swift
guard !defaults[.disableLyricsWhenPaused]
    || selectedPlayer.playbackState.isPlaying else {
    showUnavailableLyrics()
    return
}
```

Playback visibility is now controlled by the new 1.5-second runtime policy. A pause must not replace the current line with “No Available Lyrics” before the status item collapses.

- [ ] **Step 7: Run package tests and Debug build**

Run:

```bash
swift test --package-path LyricsXPackage
xcodebuild -project LyricsX.xcodeproj -scheme LyricsX -configuration Debug build
```

Expected: all package tests pass and the app builds with zero Swift compiler errors.

- [ ] **Step 8: Commit controller integration**

```bash
git add LyricsX/Controller/MenuBarLyricsController.swift
git commit -m "feat: follow playback in menu bar lyrics"
```

---

### Task 4: Regression and Manual Acceptance

**Files:**
- Modify only if verification exposes a defect in Task 1-3 files.

**Interfaces:**
- Verifies the complete feature against the approved design.

- [ ] **Step 1: Run formatting and diff checks**

Run:

```bash
swiftformat --lint LyricsXPackage/Sources/LyricsXFoundation \
  LyricsXPackage/Tests/LyricsXFoundationTests \
  LyricsX/View/CrossfadeMarqueeView.swift \
  LyricsX/Controller/MenuBarLyricsController.swift
git diff --check
```

Expected: no formatting violations and no whitespace errors.

- [ ] **Step 2: Run full automated verification**

Run:

```bash
swift test --package-path LyricsXPackage
xcodebuild -project LyricsX.xcodeproj -scheme LyricsX -configuration Debug build
xcodebuild -project LyricsX.xcodeproj -scheme LyricsX -configuration Release build
```

Expected: package tests, Debug build, and Release build all succeed.

- [ ] **Step 3: Verify short pause and resume**

With a supported player actively playing:

1. Pause for less than 1.5 seconds.
2. Resume playback.
3. Confirm the lyric status item never changes to the icon.
4. Confirm the current lyric remains visible during the brief pause.

- [ ] **Step 4: Verify sustained pause**

1. Pause for more than 1.5 seconds.
2. Confirm the lyric status item changes to the LyricsX icon.
3. Open the menu and confirm “Enable Menu Bar Lyrics” remains checked.
4. Resume playback and confirm the latest lyric appears immediately without an empty fade.

- [ ] **Step 5: Verify manual disable and launch states**

1. Disable menu bar lyrics while music is playing.
2. Pause and resume; confirm playback never re-enables lyrics.
3. Quit with music paused, relaunch, and confirm only the icon appears.
4. Quit with music playing, relaunch, and confirm lyrics appear.

- [ ] **Step 6: Verify animation and accessibility**

1. Let synchronized lyrics advance normally and confirm a subtle 0.18-second crossfade.
2. Seek rapidly across several lines and confirm only the latest line remains, with no stale completion or overlapping text.
3. Use a long line and confirm existing marquee scrolling still works at configured widths.
4. Enable macOS Reduce Motion and confirm lyric text changes immediately.

- [ ] **Step 7: Record final evidence**

Capture:

- `swift test` passed-test count.
- Debug and Release `xcodebuild` exit status.
- The player used for manual checks.
- Results for short pause, sustained pause, manual disable, rapid seek, long lyric, and Reduce Motion.

- [ ] **Step 8: Commit any verification fixes**

If verification required fixes, stage only the files changed for those fixes and commit:

```bash
git commit -m "fix: stabilize menu bar lyric transitions"
```

If no fixes were needed, do not create an empty commit.
