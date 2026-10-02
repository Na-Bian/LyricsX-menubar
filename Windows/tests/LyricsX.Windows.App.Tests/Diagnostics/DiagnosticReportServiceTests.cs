using LyricsX.Windows.App.Diagnostics;

namespace LyricsX.Windows.App.Tests.Diagnostics;

[TestClass]
public sealed class DiagnosticReportServiceTests
{
    [TestMethod]
    public void Redact_RemovesTokensAndSecrets()
    {
        var report = DiagnosticReportService.Redact(
            "usertoken=SECRET-TOKEN-123 access_token=AT-456 refresh_token=RT-789");

        Assert.IsFalse(report.Contains("SECRET-TOKEN-123"));
        Assert.IsFalse(report.Contains("AT-456"));
        Assert.IsFalse(report.Contains("RT-789"));
        StringAssert.Contains(report, "[redacted]");
    }

    [TestMethod]
    public void Redact_RemovesUserPathsAndUrls()
    {
        var report = DiagnosticReportService.Redact(
            "file=C:\\Users\\bob\\music\\song.mp3 url=https://api.example.com/v1/x?token=z");

        Assert.IsFalse(report.Contains("C:\\Users\\bob"));
        Assert.IsFalse(report.Contains("api.example.com"));
        Assert.IsFalse(report.Contains("token=z"));
    }

    [TestMethod]
    public void Redact_RemovesAuthorizationHeaders()
    {
        var report = DiagnosticReportService.Redact(
            "Authorization: Bearer sk-abc123 X-MXM-TOKEN-GUID=xyz");

        Assert.IsFalse(report.Contains("sk-abc123"));
        Assert.IsFalse(report.Contains("xyz-guid"));
        Assert.IsFalse(report.Contains("Bearer sk-abc123"));
    }

    [TestMethod]
    public void Redact_PreservesBenignContent()
    {
        var report = DiagnosticReportService.Redact(
            "provider=qq status=Playing count=3");

        StringAssert.Contains(report, "provider=qq");
        StringAssert.Contains(report, "Playing");
    }

    [TestMethod]
    public void CreateText_ContainsRequiredSectionsOnly()
    {
        var report = new DiagnosticReportBuilder(
            appVersion: "0.1.0",
            logDirectory: Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")))
            .CreateText(
                windowsVersion: "10.0.26220",
                appSdkVersion: "1.8.6",
                architecture: "ARM64",
                processUptime: TimeSpan.FromMinutes(5),
                taskbarBounds: "(0,1738)-(2940,1846) scale=2.25 autohide=False fullscreen=False",
                sourceApps: "app=Spotify.exe status=Playing",
                enabledProviders: ["qq", "netease", "kugou"],
                fingerprintPrefix: "a1b2c3",
                recentErrorEvents: ["E1201", "E1207"]);

        StringAssert.Contains(report, "0.1.0");
        StringAssert.Contains(report, "10.0.26220");
        StringAssert.Contains(report, "ARM64");
        StringAssert.Contains(report, "a1b2c3");
        StringAssert.Contains(report, "qq,netease,kugou".Replace(",", ", "));
        StringAssert.Contains(report, "E1201");
        // must not contain a lyrics or track section at all
        Assert.IsFalse(report.Contains("title="));
        Assert.IsFalse(report.Contains("lyrics="));
    }

    [TestMethod]
    public void LogRotation_TrimsOldFilesBeyondRetention()
    {
        var dir = Path.Combine(Path.GetTempPath(), "logs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            // days 0..6 are in retention; days 8 and 9 are past the boundary
            // (day 7 itself sits exactly on the cutoff, so it is avoided)
            foreach (var day in new[] { 0, 1, 2, 3, 4, 5, 6, 8, 9 })
            {
                var name = $"LyricsX-{DateTime.UtcNow.AddDays(-day):yyyyMMdd}.jsonl";
                File.WriteAllText(Path.Join(dir, name), "{}");
            }
            // over-growth file beyond the size cap
            File.WriteAllText(Path.Join(dir, "LyricsX-oversize.jsonl.1"), new string('x', 2000));

            new LogRetentionService(dir, retentionDays: 7, maxDirectoryBytes: 4096).Enforce();

            Assert.AreEqual(8, Directory.GetFiles(dir).Length); // 7 in-retention days + oversize rotated file
        }
        finally
        {
            Directory.Delete(dir, true);
        }
    }
}
