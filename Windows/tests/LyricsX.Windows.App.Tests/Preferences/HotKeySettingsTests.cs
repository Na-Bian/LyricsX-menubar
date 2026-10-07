using LyricsX.Platform.Windows.Shell;
using LyricsX.Windows.App.Preferences;

namespace LyricsX.Windows.App.Tests.Preferences;

[TestClass]
public sealed class HotKeySettingsTests
{
    [TestMethod]
    public void Parse_RoundTripsSerializedBindings()
    {
        var bindings = new[]
        {
            new HotKeyBinding(
                HotKeySettings.ShowPreferencesAction, HotKeyModifiers.Control | HotKeyModifiers.Alt, 'L'),
            new HotKeyBinding(
                HotKeySettings.ToggleVisibilityAction, HotKeyModifiers.Control | HotKeyModifiers.Shift, 'K')
        };

        var parsed = HotKeySettings.Parse(HotKeySettings.Serialize(bindings));

        Assert.AreEqual(2, parsed.Count);
        Assert.AreEqual(HotKeySettings.ShowPreferencesAction, parsed[0].ActionId);
        Assert.AreEqual(HotKeyModifiers.Control | HotKeyModifiers.Alt, parsed[0].Modifiers);
        Assert.AreEqual((uint)'L', parsed[0].VirtualKey);
        Assert.AreEqual(HotKeySettings.ToggleVisibilityAction, parsed[1].ActionId);
    }

    [TestMethod]
    public void Parse_EmptyOrNullReturnsNoBindings()
    {
        Assert.AreEqual(0, HotKeySettings.Parse(null).Count);
        Assert.AreEqual(0, HotKeySettings.Parse(string.Empty).Count);
        Assert.AreEqual(0, HotKeySettings.Parse("   ").Count);
    }

    [TestMethod]
    public void Parse_SkipsMalformedSegmentsWithoutFailing()
    {
        var parsed = HotKeySettings.Parse(
            "good:6:76|missing-parts|bad-modifier:xx:76|bad-key:6:zz|unknown-bits:9999:76|zero-modifier:0:76|another:2:65");

        Assert.AreEqual(2, parsed.Count);
        Assert.AreEqual("good", parsed[0].ActionId);
        Assert.AreEqual("another", parsed[1].ActionId);
        Assert.AreEqual((uint)65, parsed[1].VirtualKey);
    }

    [TestMethod]
    public void Serialize_EmptyListProducesEmptyString()
    {
        Assert.AreEqual(string.Empty, HotKeySettings.Serialize([]));
    }
}
