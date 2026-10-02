using Microsoft.Win32;
using LyricsX.Platform.Windows.Shell;

namespace LyricsX.Platform.Windows.Tests.Shell;

[TestClass]
public sealed class StartupRegistrationServiceTests
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "LyricsX.TestStartup";

    [TestMethod]
    public void SetEnabled_True_WritesQuotedPath()
    {
        try
        {
            var service = new StartupRegistrationService(ValueName);
            service.SetEnabled(true, @"C:\Program Files\LyricsX\LyricsX.Windows.App.exe");

            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            var value = key?.GetValue(ValueName) as string;

            Assert.IsNotNull(value);
            StringAssert.StartsWith(value, "\"");
            StringAssert.EndsWith(value, "\" --startup");
            StringAssert.Contains(value, "LyricsX.Windows.App.exe");
        }
        finally
        {
            Cleanup();
        }
    }

    [TestMethod]
    public void SetEnabled_False_RemovesOnlyOwnValue()
    {
        try
        {
            using (var writeKey = Registry.CurrentUser.CreateSubKey(RunKey))
            {
                writeKey.SetValue(ValueName, "\"stale\" --startup");
                writeKey.SetValue("LyricsX.TestOther", "keep-me");
            }

            var service = new StartupRegistrationService(ValueName);
            service.SetEnabled(false, @"C:\Anywhere\LyricsX.Windows.App.exe");

            using var readKey = Registry.CurrentUser.OpenSubKey(RunKey);
            Assert.IsNull(readKey?.GetValue(ValueName));
            Assert.IsNotNull(readKey?.GetValue("LyricsX.TestOther"));
        }
        finally
        {
            Cleanup();
        }
    }

    [TestMethod]
    public void IsEnabled_ReflectsValuePresence()
    {
        try
        {
            var service = new StartupRegistrationService(ValueName);
            Assert.IsFalse(service.IsEnabled);

            service.SetEnabled(true, @"C:\Program Files\LyricsX\LyricsX.Windows.App.exe");
            Assert.IsTrue(service.IsEnabled);

            service.SetEnabled(false, @"C:\Program Files\LyricsX\LyricsX.Windows.App.exe");
            Assert.IsFalse(service.IsEnabled);
        }
        finally
        {
            Cleanup();
        }
    }

    [TestMethod]
    public void SetEnabled_IsIdempotent()
    {
        try
        {
            var service = new StartupRegistrationService(ValueName);
            var path = @"C:\Program Files\LyricsX\LyricsX.Windows.App.exe";
            service.SetEnabled(true, path);
            service.SetEnabled(true, path);

            using var key = Registry.CurrentUser.OpenSubKey(RunKey);
            Assert.AreEqual("\"" + path + "\" --startup", key?.GetValue(ValueName));
        }
        finally
        {
            Cleanup();
        }
    }

    private static void Cleanup()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey, writable: true);
        key?.DeleteValue(ValueName, throwOnMissingValue: false);
        key?.DeleteValue("LyricsX.TestOther", throwOnMissingValue: false);
    }
}
