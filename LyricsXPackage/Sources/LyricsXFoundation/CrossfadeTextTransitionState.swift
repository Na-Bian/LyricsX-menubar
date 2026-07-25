public enum CrossfadeTextUpdate: Equatable, Sendable {
    case noChange
    case replace
    case begin
    case restart
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
        let renderableText = text.isEmpty ? " " : text

        if incomingText == renderableText || incomingText == nil && displayedText == renderableText {
            return .noChange
        }

        guard animated, displayedText != nil else {
            displayedText = renderableText
            incomingText = nil
            return .replace
        }

        if incomingText != nil {
            incomingText = renderableText
            return .restart
        }

        incomingText = renderableText
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
