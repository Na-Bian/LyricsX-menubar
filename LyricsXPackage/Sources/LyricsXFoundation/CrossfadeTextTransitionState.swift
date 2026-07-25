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
