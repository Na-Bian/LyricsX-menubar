import AppKit
import Combine
import Regex
import OpenCC
import MusicPlayer
import LyricsXFoundation

class AppController: NSObject {
    static let shared = AppController()

    var lyricsManager: LyricsProvider

    @Published var currentLyrics: Lyrics? {
        willSet {
            willChangeValue(forKey: "lyricsOffset")
            currentLineIndex = nil
        }
        didSet {
            didChangeValue(forKey: "lyricsOffset")
            scheduleCurrentLineCheck()
        }
    }

    @Published var currentLineIndex: Int?
    @Published var isSearchingLyrics = false

    var searchRequest: LyricsSearchRequest?
    private var searchKey: String?
    var searchTask: Task<Void, Never>?
    private var noLyricsRetryCounts: [String: Int] = [:]
    private let noLyricsRetryLimit = 2
    private let noLyricsRetryDelay: TimeInterval = 2
    private var unavailableDisplayRetryCounts: [String: Int] = [:]
    private let unavailableDisplayRetryLimit = 2

    private var cancelBag = Set<AnyCancellable>()

    @objc dynamic var lyricsOffset: Int {
        get {
            return currentLyrics?.offset ?? 0
        }
        set {
            currentLyrics?.offset = newValue
            currentLyrics?.metadata.needsPersist = true
            scheduleCurrentLineCheck()
        }
    }

    private override init() {
        self.lyricsManager = LyricsProviders.Group()
        super.init()
        selectedPlayer.currentTrackWillChange
            .signal()
            .receive(on: DispatchQueue.lyricsDisplay)
            .invoke(AppController.currentTrackChanged, weaklyOn: self)
            .store(in: &cancelBag)
        selectedPlayer.playbackStateWillChange
            .signal()
            .receive(on: DispatchQueue.lyricsDisplay)
            .invoke(AppController.scheduleCurrentLineCheck, weaklyOn: self)
            .store(in: &cancelBag)

        workspaceNC.publisher(for: NSWorkspace.didTerminateApplicationNotification, object: nil)
            .sink { notification in
                guard let application = notification.userInfo?[NSWorkspace.applicationUserInfoKey] as? NSRunningApplication else { return }
                let bundleID = application.bundleIdentifier
                if defaults[.launchAndQuitWithPlayer], (selectedPlayer.designatedPlayer as? MusicPlayers.Scriptable)?.playerBundleID == bundleID {
                    NSApplication.shared.terminate(self)
                }
            }.store(in: &cancelBag)
        DispatchQueue.lyricsDisplay.async { [weak self] in
            self?.currentTrackChanged()
        }

        Task {
            try await updateLyricsManager()
            if currentLyrics == nil, selectedPlayer.currentTrack != nil {
                DispatchQueue.lyricsDisplay.async { [weak self] in
                    self?.currentTrackChanged()
                }
            }
        }
    }

    @MainActor
    func updateLyricsManager() async throws {
        let services: [LyricsProviders.Service] = LyricsProviders.Service.noAuthenticationRequiredServices

        var providers: [LyricsProvider] = []
        for service in services {
            providers.append(service.create())
        }

        // Add Musixmatch provider with saved token if available
        if let token = defaults[.musixmatchToken], !token.isEmpty {
            let musixmatchProvider = LyricsProviders.Musixmatch(usertoken: token)
            providers.append(musixmatchProvider)
        }

        lyricsManager = LyricsProviders.Group(providers: providers)
    }

    var currentLineCheckSchedule: Cancellable?

    func scheduleCurrentLineCheck() {
        currentLineCheckSchedule?.cancel()
        guard let lyrics = currentLyrics else {
            return
        }
        let playbackState = MusicPlayers.Selected.shared.playbackState
        let playbackTime = playbackState.time
        let (index, next) = lyrics[playbackTime + lyrics.adjustedTimeDelay]
        if currentLineIndex != index {
            currentLineIndex = index
        }
        if let next = next, playbackState.isPlaying {
            let dt = lyrics.lines[next].position - playbackTime - lyrics.adjustedTimeDelay
            let q = DispatchQueue.lyricsDisplay
            currentLineCheckSchedule = q.schedule(after: q.now.advanced(by: .seconds(dt)), interval: .seconds(42), tolerance: .milliseconds(20)) { [unowned self] in
                self.scheduleCurrentLineCheck()
            }
        }
    }

    func writeToiTunes(overwrite: Bool) {
        guard selectedPlayer.name == .appleMusic,
              let currentLyrics = currentLyrics,
              let sbTrack = selectedPlayer.currentTrack?.originalTrack,
              overwrite || (sbTrack.value(forKey: "lyrics") as! String?)?.isEmpty != false else {
            return
        }

        let content: String
        if defaults[.writeiTunesConvertToPlainLRC] {
            // For plain LRC export, preserve the legacy LRC formatting but still respect
            // the Chinese conversion setting for consistency with the non-plain branch.
            var legacy = currentLyrics.legacyDescription
            if let converter = ChineseConverter.shared {
                legacy = converter.convert(legacy)
            }
            // Note: translations are intentionally not appended for plain LRC export,
            // even when `writeiTunesWithTranslation` is enabled, to keep the legacy
            // LRC output single-line per timestamp.
            content = legacy
        } else {
            content = currentLyrics.lines.map { line -> String in
                var content = line.content
                if let converter = ChineseConverter.shared {
                    content = converter.convert(content)
                }
                if defaults[.writeiTunesWithTranslation] {
                    // TODO: tagged translation
                    let code = currentLyrics.metadata.translationLanguages.first
                    if var translation = line.attachments[.translation(languageCode: code)] {
                        if let converter = ChineseConverter.shared {
                            translation = converter.convert(translation)
                        }
                        content += "\n" + translation
                    }
                }
                return content
            }.joined(separator: "\n")
        }
        // swiftlint:disable:next force_try
        let regex = Regex(#"\n{3,}"#)
        let replaced = content.replacingMatches(of: regex, with: "\n\n")
        sbTrack.setValue(replaced, forKey: "lyrics")
    }

    func currentTrackChanged() {
        if currentLyrics?.metadata.needsPersist == true {
            currentLyrics?.persist()
        }
        currentLyrics = nil
        currentLineIndex = nil
        isSearchingLyrics = false
        searchRequest = nil
        searchKey = nil
        searchTask?.cancel()
        guard let track = selectedPlayer.currentTrack else {
            return
        }
        let trackSearchKey = lyricsSearchKey(for: track)
        noLyricsRetryCounts[trackSearchKey] = 0
        unavailableDisplayRetryCounts[trackSearchKey] = 0
        // FIXME: deal with optional value
        let title = track.title ?? ""
        let artist = track.artist ?? ""

        guard !defaults[.noSearchingTrackIds].contains(track.id) else {
            return
        }

        if let manualLyrics = manualLyrics(for: track) {
            currentLyrics = manualLyrics
            return
        }

        var candidateLyricsURL: [(URL, Bool, Bool)] = [] // (fileURL, isSecurityScoped, needsSearching)

        if defaults[.loadLyricsBesideTrack] {
            if let embeddedLyrics = track.lyrics, !embeddedLyrics.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty {
                if let lyrics = Lyrics(embeddedLyrics) {
                    if lyrics.metadata.title == nil || lyrics.metadata.title?.isEmpty == true {
                        lyrics.metadata.title = title
                    }
                    if lyrics.metadata.artist == nil || lyrics.metadata.artist?.isEmpty == true {
                        lyrics.metadata.artist = artist
                    }
                    lyrics.filtrate()
                    lyrics.recognizeLanguage()
                    if hasDisplayableLyrics(lyrics) {
                        currentLyrics = lyrics
                        return
                    }
                }
            }
            if let fileName = track.localFileURL?.deletingPathExtension() {
                candidateLyricsURL += [
                    (fileName.appendingPathExtension("lrcx"), false, false),
                    (fileName.appendingPathExtension("lrc"), false, false),
                ]
            }
        }

        let (url, security) = defaults.lyricsSavingPath()
        let titleForReading = title.replacingOccurrences(of: "/", with: ":")
        let artistForReading = artist.replacingOccurrences(of: "/", with: ":")
        let fileName = url.appendingPathComponent("\(titleForReading) - \(artistForReading)")
        candidateLyricsURL += [
            (fileName.appendingPathExtension("lrcx"), security, false),
            (fileName.appendingPathExtension("lrc"), security, true),
        ]

        for (url, security, needsSearching) in candidateLyricsURL {
            if security {
                guard url.startAccessingSecurityScopedResource() else {
                    continue
                }
            }
            defer {
                if security {
                    url.stopAccessingSecurityScopedResource()
                }
            }

            if let lrcContents = try? String(contentsOf: url, encoding: String.Encoding.utf8),
               let lyrics = Lyrics(lrcContents) {
                lyrics.metadata.localURL = url
                lyrics.metadata.title = title
                lyrics.metadata.artist = artist
                lyrics.filtrate()
                lyrics.recognizeLanguage()
                guard hasDisplayableLyrics(lyrics) else {
                    continue
                }
                currentLyrics = lyrics
                if needsSearching {
                    break
                } else {
                    return
                }
            }
        }

        if let album = track.album, defaults[.noSearchingAlbumNames].contains(album) {
            return
        }

        startLyricsSearch(for: track, searchKey: trackSearchKey)
    }

    func retryLyricsSearchForUnavailableMenuBar() {
        guard let track = selectedPlayer.currentTrack else {
            return
        }

        let searchKey = lyricsSearchKey(for: track)
        guard selectedPlayer.currentTrack.map(lyricsSearchKey(for:)) == searchKey,
              !isSearchingLyrics,
              currentLyrics.map(hasDisplayableLyrics(_:)) != true,
              !defaults[.noSearchingTrackIds].contains(track.id),
              defaults[.noSearchingAlbumNames].contains(track.album ?? "") == false else {
            return
        }

        let retryCount = unavailableDisplayRetryCounts[searchKey] ?? 0
        guard retryCount < unavailableDisplayRetryLimit else {
            return
        }

        unavailableDisplayRetryCounts[searchKey] = retryCount + 1
        searchTask?.cancel()
        startLyricsSearch(for: track, searchKey: searchKey)
    }

    private func startLyricsSearch(for track: MusicTrack, searchKey: String) {
        let duration = track.duration ?? 0
        let title = track.title ?? ""
        let artist = track.artist ?? ""
        let request = LyricsSearchRequest(searchTerm: .info(title: title, artist: artist), duration: duration, limit: 5)
        searchRequest = request
        self.searchKey = searchKey
        isSearchingLyrics = true
        searchTask = Task { @MainActor in
            defer {
                if self.searchRequest == request,
                   self.searchKey == searchKey {
                    self.isSearchingLyrics = false
                }
            }
            do {
                // Accept the first arrived lyrics immediately,
                // but keep collecting for a short window to allow higher-priority providers,
                // which might be slower, to replace it.
                let window = defaults[.lyricsPriorityWindow] ?? 5 // seconds
                var firstReceived = false
                var collectionStart: Date?

                for try await lyrics in lyricsManager.lyrics(for: request) {
                    guard selectedPlayer.currentTrack.map(lyricsSearchKey(for:)) == searchKey,
                          self.searchRequest == request,
                          self.searchKey == searchKey else {
                        return
                    }

                    if !firstReceived {
                        let accepted = lyricsReceived(lyrics: lyrics)
                        if accepted, let current = currentLyrics, current === lyrics {
                            firstReceived = true
                            collectionStart = Date()
                        }
                        continue
                    }

                    if let start = collectionStart,
                       Date().timeIntervalSince(start) <= window {
                        lyricsReceived(lyrics: lyrics)
                        continue
                    } else {
                        // window expired
                        break
                    }
                }

                guard selectedPlayer.currentTrack.map(lyricsSearchKey(for:)) == searchKey,
                      self.searchRequest == request,
                      self.searchKey == searchKey else {
                    return
                }

                if defaults[.writeToiTunesAutomatically],
                   currentLyrics.map(hasDisplayableLyrics(_:)) == true {
                    writeToiTunes(overwrite: true)
                }
                scheduleRetryIfNeeded(for: track, searchKey: searchKey)
            } catch is CancellationError {
                // Search was cancelled due to track change
            } catch {
                print("Failed to fetch lyrics: \(error.localizedDescription)")
                scheduleRetryIfNeeded(for: track, searchKey: searchKey)
            }
        }
    }

    @MainActor
    private func scheduleRetryIfNeeded(for track: MusicTrack, searchKey: String) {
        guard currentLyrics.map(hasDisplayableLyrics(_:)) != true,
              selectedPlayer.currentTrack.map(lyricsSearchKey(for:)) == searchKey,
              !defaults[.noSearchingTrackIds].contains(track.id),
              defaults[.noSearchingAlbumNames].contains(track.album ?? "") == false else {
            return
        }

        let retryCount = noLyricsRetryCounts[searchKey] ?? 0
        guard retryCount < noLyricsRetryLimit else {
            return
        }

        noLyricsRetryCounts[searchKey] = retryCount + 1
        searchTask = Task { @MainActor in
            try? await Task.sleep(nanoseconds: UInt64(noLyricsRetryDelay * 1_000_000_000))
            guard currentLyrics.map(hasDisplayableLyrics(_:)) != true,
                  selectedPlayer.currentTrack.map(lyricsSearchKey(for:)) == searchKey else {
                return
            }
            startLyricsSearch(for: track, searchKey: searchKey)
        }
    }

    // MARK: LyricsSourceDelegate

    @discardableResult
    func lyricsReceived(lyrics: Lyrics) -> Bool {
        guard let req = searchRequest,
              lyrics.metadata.request == req,
              let track = selectedPlayer.currentTrack,
              selectedPlayer.currentTrack.map(lyricsSearchKey(for:)) == searchKey else {
            return false
        }
        if defaults[.strictSearchEnabled], !lyrics.isMatched() {
            return false
        }

        lyrics.associateWithTrack(track)
        lyrics.filtrate()
        lyrics.recognizeLanguage()
        guard hasDisplayableLyrics(lyrics) else {
            return false
        }
        if let current = currentLyrics,
           hasDisplayableLyrics(current),
           !lyricsHasHigherPriority(lyrics, over: current) {
            return false
        }

        lyrics.metadata.needsPersist = true
        currentLyrics = lyrics
        return true
    }

    func useManualLyrics(_ lyrics: Lyrics, for track: MusicTrack) {
        searchTask?.cancel()
        searchTask = nil
        searchRequest = nil
        searchKey = nil
        isSearchingLyrics = false
        allowSearching(track)

        lyrics.associateWithTrack(track)
        lyrics.filtrate()
        lyrics.recognizeLanguage()
        lyrics.metadata.needsPersist = true
        currentLyrics = lyrics

        let fileName = manualLyricsFileName(for: track)
        lyrics.persist(fileName: fileName)
        lyrics.persist()

        var manualLyricsFileNames = defaults[.manualLyricsFileNamesByTrackID]
        manualLyricsFileNames[track.id] = fileName
        defaults[.manualLyricsFileNamesByTrackID] = manualLyricsFileNames

        var manualLyricsFileNamesByFingerprint = defaults[.manualLyricsFileNamesByTrackFingerprint]
        manualLyricsFileNamesByFingerprint[manualLyricsFingerprint(for: track)] = fileName
        defaults[.manualLyricsFileNamesByTrackFingerprint] = manualLyricsFileNamesByFingerprint
        defaults.synchronize()
    }

    private func allowSearching(_ track: MusicTrack) {
        if let index = defaults[.noSearchingTrackIds].firstIndex(of: track.id) {
            defaults[.noSearchingTrackIds].remove(at: index)
        }
        if let index = defaults[.noSearchingAlbumNames].firstIndex(of: track.album ?? "") {
            defaults[.noSearchingAlbumNames].remove(at: index)
        }
    }

    private func manualLyrics(for track: MusicTrack) -> Lyrics? {
        let fileName = defaults[.manualLyricsFileNamesByTrackID][track.id]
            ?? defaults[.manualLyricsFileNamesByTrackFingerprint][manualLyricsFingerprint(for: track)]

        guard let fileName else {
            return nil
        }

        let (folderURL, security) = defaults.lyricsSavingPath()
        let fileURL = folderURL.appendingPathComponent(fileName)
        return loadLyrics(from: fileURL, security: security, for: track)
    }

    private func loadLyrics(from url: URL, security: Bool, for track: MusicTrack) -> Lyrics? {
        if security {
            guard url.startAccessingSecurityScopedResource() else {
                return nil
            }
        }
        defer {
            if security {
                url.stopAccessingSecurityScopedResource()
            }
        }

        guard let lrcContents = try? String(contentsOf: url, encoding: .utf8),
              let lyrics = Lyrics(lrcContents) else {
            return nil
        }

        lyrics.metadata.localURL = url
        lyrics.associateWithTrack(track)
        lyrics.filtrate()
        lyrics.recognizeLanguage()
        guard hasDisplayableLyrics(lyrics) else {
            return nil
        }
        return lyrics
    }

    private func manualLyricsFileName(for track: MusicTrack) -> String {
        let title = sanitizedLyricsFileNameComponent(track.title, fallback: "Unknown")
        let artist = sanitizedLyricsFileNameComponent(track.artist, fallback: "Unknown")
        let trackID = sanitizedLyricsFileNameComponent(track.id, fallback: "\(title)-\(artist)")
        let shortTrackID = String(trackID.prefix(48))
        return "\(title) - \(artist) [manual-\(shortTrackID)].lrcx"
    }

    private func hasDisplayableLyrics(_ lyrics: Lyrics) -> Bool {
        lyrics.lines.contains { line in
            line.enabled && !line.content.trimmingCharacters(in: .whitespacesAndNewlines).isEmpty
        }
    }

    private func lyricsSearchKey(for track: MusicTrack) -> String {
        [
            normalizedManualLyricsKeyComponent(track.title),
            normalizedManualLyricsKeyComponent(track.artist),
            normalizedManualLyricsKeyComponent(track.album),
            track.duration.map { String(Int($0.rounded())) } ?? "",
        ].joined(separator: "\u{1f}")
    }

    private func manualLyricsFingerprint(for track: MusicTrack) -> String {
        [
            normalizedManualLyricsKeyComponent(track.title),
            normalizedManualLyricsKeyComponent(track.artist),
            normalizedManualLyricsKeyComponent(track.album),
        ].joined(separator: "\u{1f}")
    }

    private func normalizedManualLyricsKeyComponent(_ string: String?) -> String {
        string?
            .trimmingCharacters(in: .whitespacesAndNewlines)
            .folding(options: [.caseInsensitive, .diacriticInsensitive, .widthInsensitive], locale: .current)
            ?? ""
    }

    private func sanitizedLyricsFileNameComponent(_ string: String?, fallback: String) -> String {
        guard let string else {
            return fallback
        }

        let sanitized = string
            .trimmingCharacters(in: .whitespacesAndNewlines)
            .map { character -> Character in
                switch character {
                case "/", ":", "\\", "\0":
                    return "-"
                default:
                    return character
                }
            }

        let value = String(sanitized).trimmingCharacters(in: .whitespacesAndNewlines)
        return value.isEmpty ? fallback : value
    }
}

extension AppController {
    func importLyrics(_ lyricsString: String) throws {
        guard let lrc = Lyrics(lyricsString) else {
            let errorInfo = [
                NSLocalizedDescriptionKey: "Invalid lyric file",
                NSLocalizedRecoverySuggestionErrorKey: "Please try another one.",
            ]
            let error = NSError(domain: lyricsXErrorDomain, code: 0, userInfo: errorInfo)
            throw error
        }
        guard let track = selectedPlayer.currentTrack else {
            let errorInfo = [
                NSLocalizedDescriptionKey: "No music playing",
                NSLocalizedRecoverySuggestionErrorKey: "Play a music and try again.",
            ]
            let error = NSError(domain: lyricsXErrorDomain, code: 0, userInfo: errorInfo)
            throw error
        }
        useManualLyrics(lrc, for: track)
    }
}
