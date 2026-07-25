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
        let renderableValue = transitionState.incomingText
            ?? transitionState.displayedText
            ?? value

        switch update {
        case .noChange:
            let contentLabel = transitionState.incomingText == nil
                ? currentContentLabel
                : incomingContentLabel
            contentLabel.setStringValue(
                renderableValue,
                lineDisplayTime: lineDisplayTime
            )
        case .replace:
            renderImmediately(renderableValue, lineDisplayTime: lineDisplayTime)
        case .begin:
            beginCrossfade(renderableValue, lineDisplayTime: lineDisplayTime)
        case .restart:
            restartCrossfade(renderableValue, lineDisplayTime: lineDisplayTime)
        }
    }

    func stopAnimations() {
        transitionGeneration &+= 1
        labels.forEach { $0.layer?.removeAllAnimations() }
        if transitionState.incomingText != nil {
            activeIndex = 1 - activeIndex
            transitionState.completeTransition()
        }
        labels.forEach { $0.alphaValue = 0 }
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

    private func restartCrossfade(
        _ value: String,
        lineDisplayTime: TimeInterval
    ) {
        transitionGeneration &+= 1
        labels.forEach { $0.layer?.removeAllAnimations() }
        currentContentLabel.alphaValue = 1
        incomingContentLabel.alphaValue = 0
        beginCrossfade(value, lineDisplayTime: lineDisplayTime)
    }
}
