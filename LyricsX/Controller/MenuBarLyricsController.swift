import AppKit
import Combine
import GenericID
import LyricsXFoundation
import MusicPlayer
import OpenCC
import SwiftCF
import AccessibilityExt
import MarqueeLabel

class MenuBarLyricsController {
    static let shared = MenuBarLyricsController()

    var statusBarMenu: NSMenu? {
        didSet {
            setupStatusItemMenu()
        }
    }

    private var iconStatusItem: NSStatusItem?
    private var lyricStatusItem: NSStatusItem?
    private let lyricItemLength: CGFloat = 142

    private lazy var marqueeLabel = MarqueeLabel(frame: .init(x: 0, y: 0, width: lyricItemLength, height: 22))

    private static let defaultLyric = "LyricsX"

    private var screenLyrics: (lyrics: String, duration: TimeInterval) = (MenuBarLyricsController.defaultLyric, 2) {
        didSet {
            DispatchQueue.main.async {
                self.updateStatusItems()
            }
        }
    }

    private var cancelBag = Set<AnyCancellable>()

    private init() {
        if !defaults[.hideMenuBarItems] {
            updateStatusItems()
        }
        AppController.shared.$currentLyrics
            .combineLatest(AppController.shared.$currentLineIndex)
            .receive(on: DispatchQueue.lyricsDisplay)
            .invoke(MenuBarLyricsController.handleLyricsDisplay, weaklyOn: self)
            .store(in: &cancelBag)
        workspaceNC
            .publisher(for: NSWorkspace.didActivateApplicationNotification)
            .signal()
            .invoke(MenuBarLyricsController.updateStatusItems, weaklyOn: self)
            .store(in: &cancelBag)
        defaults.publisher(for: [.menuBarLyricsEnabled, .combinedMenubarLyrics, .hideMenuBarItems])
            .prepend()
            .invoke(MenuBarLyricsController.updateStatusItems, weaklyOn: self)
            .store(in: &cancelBag)
    }

    private func handleLyricsDisplay(event: (lyrics: Lyrics?, index: Int?)) {
        guard !defaults[.disableLyricsWhenPaused] || selectedPlayer.playbackState.isPlaying,
              let lyrics = event.lyrics,
              let index = event.index else {
//            screenLyrics = (MenuBarLyricsController.defaultLyric, 2)
            return
        }
        let currentLine = lyrics.lines[index]
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
        } else if let nextLine = lyrics.lines[safe: index + 1] {
            lineDisplayTime = nextLine.position - currentLine.position
        } else {
            lineDisplayTime = 2
        }
        screenLyrics = (newScreenLyrics, lineDisplayTime)
    }

    @objc private func updateStatusItems() {
        guard !defaults[.hideMenuBarItems] else {
            marqueeLabel.removeFromSuperview()
            iconStatusItem = nil
            lyricStatusItem = nil
            return
        }

        if lyricStatusItem == nil || iconStatusItem != nil {
            iconStatusItem = nil
            setupLyricStatusItem()
        }

        if defaults[.menuBarLyricsEnabled] {
            marqueeLabel.setStringValue(screenLyrics.lyrics, lineDisplayTime: screenLyrics.duration)
        } else {
            marqueeLabel.setStringValue(MenuBarLyricsController.defaultLyric, lineDisplayTime: 2)
        }
    }

    private func setupLyricStatusItem() {
        marqueeLabel.removeFromSuperview()
        lyricStatusItem = NSStatusBar.system.statusItem(withLength: NSStatusItem.variableLength)
        lyricStatusItem?.button?.title = ""
        lyricStatusItem?.button?.image = nil
        lyricStatusItem?.length = lyricItemLength
        marqueeLabel.frame = .init(x: 0, y: 0, width: lyricItemLength, height: 22)
        lyricStatusItem?.button?.frame = marqueeLabel.bounds
        lyricStatusItem?.button?.addSubview(marqueeLabel)
        setupStatusItemMenu()
    }

    private func setupStatusItemMenu() {
        iconStatusItem?.menu = nil
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
