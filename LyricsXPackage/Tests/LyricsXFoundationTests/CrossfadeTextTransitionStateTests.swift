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

    @Test func rapidUpdateRestartsTowardsLatestText() {
        var state = CrossfadeTextTransitionState(displayedText: "First")
        _ = state.update(to: "Second", animated: true)

        let update = state.update(to: "Latest", animated: true)
        #expect(update == .restart)
        #expect(state.displayedText == "First")
        #expect(state.incomingText == "Latest")

        state.completeTransition()
        #expect(state.displayedText == "Latest")
        #expect(state.incomingText == nil)
    }

    @Test func emptyTextUsesRenderableBlank() {
        var state = CrossfadeTextTransitionState(displayedText: "First")

        #expect(state.update(to: "", animated: true) == .begin)
        #expect(state.displayedText == "First")
        #expect(state.incomingText == " ")

        state.completeTransition()
        #expect(state.displayedText == " ")
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
