import AppKit
import Combine
import GenericID
import LyricsXFoundation
import MusicPlayer
import OpenCC
import SwiftCF
import AccessibilityExt

class MenuBarLyricsController {
    static let shared = MenuBarLyricsController()

    var statusBarMenu: NSMenu? {
        didSet {
            setupStatusItemMenu()
        }
    }

    private var iconStatusItem: NSStatusItem?
    private var lyricStatusItem: NSStatusItem?
    private var buttonImage = #imageLiteral(resourceName: "status_bar_icon")
    private var buttonLength: CGFloat = 30
    private var lyricItemLength: CGFloat {
        min(max(defaults[.menuBarLyricsWidth], 60), 240)
    }

    private lazy var marqueeView = CrossfadeMarqueeView(
        frame: .init(x: 0, y: 0, width: lyricItemLength, height: 22)
    )
    private let visibilityPolicy = MenuBarLyricsVisibilityPolicy()
    private var pendingHideWorkItem: DispatchWorkItem?

    private static let defaultLyric = "LyricsX"
    private static let unavailableLyric = NSLocalizedString("No Available Lyrics", comment: "Menu bar text shown when the current track has no lyrics")
    private static let searchingLyric = NSLocalizedString("Searching Lyrics...", comment: "Menu bar text shown while LyricsX is searching lyrics for the current track")

    private var screenLyrics: (lyrics: String, duration: TimeInterval) = (MenuBarLyricsController.defaultLyric, 2) {
        didSet {
            DispatchQueue.main.async { [weak self] in
                self?.refreshLyricsText(animated: true)
            }
        }
    }

    private var cancelBag = Set<AnyCancellable>()

    private init() {
        AppController.shared.$currentLyrics
            .combineLatest(AppController.shared.$currentLineIndex, AppController.shared.$isSearchingLyrics)
            .receive(on: DispatchQueue.lyricsDisplay)
            .invoke(MenuBarLyricsController.handleLyricsDisplay, weaklyOn: self)
            .store(in: &cancelBag)

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
    }

    deinit {
        pendingHideWorkItem?.cancel()
    }

    private func handleLyricsDisplay(event: (lyrics: Lyrics?, index: Int?, isSearching: Bool)) {
        guard let lyrics = event.lyrics else {
            if event.isSearching {
                updateScreenLyrics(to: MenuBarLyricsController.searchingLyric, duration: 2)
            } else {
                showUnavailableLyrics()
                AppController.shared.retryLyricsSearchForUnavailableMenuBar()
            }
            return
        }

        guard let (currentLine, currentIndex) = currentLine(from: lyrics, index: event.index) else {
            showUnavailableLyrics()
            AppController.shared.retryLyricsSearchForUnavailableMenuBar()
            return
        }

        var newScreenLyrics = currentLine.content
        if let converter = ChineseConverter.shared, lyrics.metadata.language?.hasPrefix("zh") == true {
            newScreenLyrics = converter.convert(newScreenLyrics)
        }
        if newScreenLyrics == screenLyrics.lyrics {
            return
        }
        let lineDisplayTime: TimeInterval
        if let duration = currentLine.attachments.timetag?.duration {
            lineDisplayTime = duration
        } else if let nextLine = lyrics.lines[safe: currentIndex + 1] {
            lineDisplayTime = nextLine.position - currentLine.position
        } else {
            lineDisplayTime = 2
        }
        screenLyrics = (newScreenLyrics, lineDisplayTime)
    }

    private func currentLine(from lyrics: Lyrics, index: Int?) -> (line: LyricsLine, index: Int)? {
        if let index,
           let line = lyrics.lines[safe: index],
           line.enabled {
            return (line, index)
        }

        guard let fallbackIndex = lyrics.lines.firstIndex(where: { $0.enabled && !$0.content.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty }) else {
            return nil
        }
        return (lyrics.lines[fallbackIndex], fallbackIndex)
    }

    private func showUnavailableLyrics() {
        updateScreenLyrics(to: MenuBarLyricsController.unavailableLyric, duration: 2)
    }

    private func updateScreenLyrics(to lyrics: String, duration: TimeInterval) {
        guard screenLyrics.lyrics != lyrics || screenLyrics.duration != duration else {
            return
        }
        screenLyrics = (lyrics, duration)
    }

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

    private func setupLyricStatusItem() {
        marqueeView.removeFromSuperview()
        lyricStatusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        lyricStatusItem?.button?.title = ""
        lyricStatusItem?.button?.image = nil
        lyricStatusItem?.length = lyricItemLength
        marqueeView.frame = .init(x: 0, y: 0, width: lyricItemLength, height: 22)
        lyricStatusItem?.button?.frame = marqueeView.bounds
        lyricStatusItem?.button?.addSubview(marqueeView)
        setupStatusItemMenu()
    }

    private func setupIconStatusItem() {
        iconStatusItem = NSStatusBar.system.statusItem(withLength: buttonLength)
        iconStatusItem?.button?.title = ""
        iconStatusItem?.button?.image = buttonImage
        iconStatusItem?.length = buttonLength
        setupStatusItemMenu()
    }

    private func removeIconStatusItem() {
        if let iconStatusItem {
            NSStatusBar.system.removeStatusItem(iconStatusItem)
        }
        iconStatusItem = nil
    }

    private func removeLyricStatusItem() {
        marqueeView.stopAnimations()
        marqueeView.removeFromSuperview()
        if let lyricStatusItem {
            NSStatusBar.system.removeStatusItem(lyricStatusItem)
        }
        lyricStatusItem = nil
    }

    private func setupStatusItemMenu() {
        iconStatusItem?.menu = statusBarMenu
        lyricStatusItem?.menu = statusBarMenu
    }
}

// MARK: - Status Item Visibility

extension NSStatusItem {
    fileprivate var isVisibe: Bool {
        guard let buttonFrame = button?.frame,
              let frame = button?.window?.convertToScreen(buttonFrame) else {
            return false
        }

        let point = CGPoint(x: frame.midX, y: frame.midY)
        guard let screen = NSScreen.screens.first(where: { $0.frame.contains(point) }) else {
            return false
        }
        let carbonPoint = CGPoint(x: point.x, y: screen.frame.height - point.y - 1)

        guard let element = try? AXUIElement.systemWide().element(at: carbonPoint),
              let pid = try? element.pid() else {
            return false
        }

        return getpid() == pid
    }
}

extension String {
    fileprivate func components(options: String.EnumerationOptions) -> [String] {
        var components: [String] = []
        let range = Range(uncheckedBounds: (startIndex, endIndex))
        enumerateSubstrings(in: range, options: options) { _, _, range, _ in
            components.append(String(self[range]))
        }
        return components
    }
}

extension Array {
    subscript(safe safeIndex: Int) -> Element? {
        if safeIndex >= 0, safeIndex < count {
            return self[safeIndex]
        } else {
            return nil
        }
    }
}
