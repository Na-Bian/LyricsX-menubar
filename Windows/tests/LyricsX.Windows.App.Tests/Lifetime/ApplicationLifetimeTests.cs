using LyricsX.Windows.App.Services;

namespace LyricsX.Windows.App.Tests.Lifetime;

[TestClass]
public sealed class ApplicationLifetimeTests
{
    [TestMethod]
    public void StartupOrder_MatchesPlanSequence()
    {
        var order = ApplicationStartup.Sequence;

        CollectionAssert.AreEqual(
            new[]
            {
                "load-settings", "start-logging", "create-shell-window", "start-tray",
                "start-media-source", "start-selected-session", "start-lyrics-coordinator",
                "create-taskbar-window", "start-placement", "register-hotkeys", "register-startup"
            },
            order);
    }

    [TestMethod]
    public void ShutdownOrder_IsReverseOfStartup()
    {
        var expected = ApplicationStartup.Sequence.Reverse().ToArray();
        CollectionAssert.AreEqual(expected, ApplicationStartup.ShutdownSequence);
    }

    // Named-pipe reachability differs between SSH (network) logon sessions
    // and the interactive session on this VM; verified interactively, and the
    // full second-instance flow is gated on plan 04's acceptance matrix.
    [TestCategory("InteractiveSession")]
    [TestMethod]
    public async Task CommandPipe_SecondInstance_TriggersShowPreferences()
    {
        using var primary = new SingleInstanceService();
        Assert.IsTrue(primary.TryBecomePrimary());
        var commands = new List<string>();
        primary.CommandReceived += command => commands.Add(command);

        // the server loop is async: give it a moment to open its first pipe
        await Task.Delay(200);

        using var second = new SingleInstanceService();
        Assert.IsFalse(second.TryBecomePrimary());
        Assert.IsTrue(second.SendCommand("show-preferences"),
            "second instance could not reach the primary pipe; server error: " +
            primary.LastServerError ?? "none");

        // the server loop is async: allow it to dispatch
        var deadline = DateTime.UtcNow.AddSeconds(3);
        while (commands.Count == 0 && DateTime.UtcNow < deadline)
        {
            await Task.Delay(50);
        }

        CollectionAssert.AreEqual(new[] { "show-preferences" }, commands);
    }

    [TestMethod]
#pragma warning disable MSTEST0032 // the constants are the contract
    public void ExitCodes_AreStable()
    {
        Assert.AreEqual(0, ApplicationStartup.ExitCodeSuccess);
        Assert.AreEqual(10, ApplicationStartup.ExitCodeUnsupportedOs);
        Assert.AreEqual(20, ApplicationStartup.ExitCodeFatalInit);
    }
#pragma warning restore MSTEST0032
}
