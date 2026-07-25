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
