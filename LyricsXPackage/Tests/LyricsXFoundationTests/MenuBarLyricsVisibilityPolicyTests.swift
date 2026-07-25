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
