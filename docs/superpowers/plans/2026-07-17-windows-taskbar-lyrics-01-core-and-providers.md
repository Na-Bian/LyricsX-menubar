# Windows Taskbar Lyrics 01: Core and Providers Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Build a platform-neutral lyrics engine with parsing, synchronization, matching, caching, manual associations, and the five approved online providers.

**Architecture:** `LyricsX.Core` owns immutable domain models and deterministic algorithms. `LyricsX.Providers` owns HTTP/authentication adapters and maps every remote response into core models. Both projects are independent from WinUI and Windows media APIs so almost all behavior is covered by fast tests.

**Tech Stack:** .NET 10 LTS, C# 14, MSTest 4.3.2, Microsoft.NET.Test.Sdk 18.8.1, coverlet.collector 10.0.1, OpenccNetLib 1.6.1, `System.Text.Json`, `HttpClient`.

## Global Constraints

- Work only under `Windows/`; do not modify the existing macOS targets.
- Target `net10.0-windows10.0.22000.0` and `win-x64`.
- Keep domain code free of WinUI, HWND, registry, UI Automation, and GSMTC types.
- Use cancellation tokens on all I/O and long-running searches.
- Never log OAuth tokens, cookies, authorization headers, or complete provider responses.
- Store fixtures with invented song metadata or public-domain lyric fragments.
- Keep Spotify disabled until the user supplies credentials through the settings UI.
- Commit only files named by the current task.

## Stable Cross-Plan Contracts

These types are the boundary consumed by plans 02 and 03:

```csharp
namespace LyricsX.Core;

public enum PlaybackStatus { Closed, Stopped, Paused, Playing }

public sealed record MediaTrack(
    string SourceAppId,
    string TrackId,
    string Title,
    string Artist,
    string? Album,
    TimeSpan? Duration);

public sealed record PlaybackSnapshot(
    MediaTrack? Track,
    PlaybackStatus Status,
    TimeSpan Position,
    double Rate,
    DateTimeOffset ObservedAt);

public sealed record LyricWord(
    TimeSpan Position,
    TimeSpan? Duration,
    string Content);

public sealed record LyricLine(
    TimeSpan Position,
    TimeSpan? Duration,
    string Content,
    bool Enabled = true,
    IReadOnlyList<LyricWord>? Words = null);

public sealed record LyricMetadata(
    string? Title,
    string? Artist,
    string? Album,
    string? Author,
    TimeSpan? Duration,
    IReadOnlyDictionary<string, string> Extra);

public sealed record LyricDocument(
    IReadOnlyList<LyricLine> Lines,
    LyricMetadata Metadata,
    TimeSpan Delay);

public sealed record LyricSearchRequest(MediaTrack Track, int Limit = 5);

public sealed record LyricCandidate(
    string ProviderId,
    LyricDocument Document,
    string Title,
    string Artist,
    string? Album,
    TimeSpan? Duration,
    int ProviderRank);

public interface ILyricsProvider
{
    string Id { get; }
    IAsyncEnumerable<LyricCandidate> SearchAsync(
        LyricSearchRequest request,
        CancellationToken cancellationToken);
}
```

---

## Task 1: Scaffold the Windows solution and pin dependencies

**Files:**

- Create: `Windows/LyricsX.Windows.slnx`
- Create: `Windows/Directory.Build.props`
- Create: `Windows/Directory.Packages.props`
- Create: `Windows/src/LyricsX.Core/LyricsX.Core.csproj`
- Create: `Windows/src/LyricsX.Providers/LyricsX.Providers.csproj`
- Create: `Windows/tests/LyricsX.Core.Tests/LyricsX.Core.Tests.csproj`
- Create: `Windows/tests/LyricsX.Providers.Tests/LyricsX.Providers.Tests.csproj`

**Interfaces produced:** build graph and centrally pinned packages.

- [ ] Create the projects and solution:

```powershell
Set-Location Windows
dotnet new sln --name LyricsX.Windows --format slnx
dotnet new classlib --name LyricsX.Core --output src/LyricsX.Core --framework net10.0
dotnet new classlib --name LyricsX.Providers --output src/LyricsX.Providers --framework net10.0
dotnet new mstest --name LyricsX.Core.Tests --output tests/LyricsX.Core.Tests --framework net10.0
dotnet new mstest --name LyricsX.Providers.Tests --output tests/LyricsX.Providers.Tests --framework net10.0
dotnet sln LyricsX.Windows.slnx add src/LyricsX.Core/LyricsX.Core.csproj
dotnet sln LyricsX.Windows.slnx add src/LyricsX.Providers/LyricsX.Providers.csproj
dotnet sln LyricsX.Windows.slnx add tests/LyricsX.Core.Tests/LyricsX.Core.Tests.csproj
dotnet sln LyricsX.Windows.slnx add tests/LyricsX.Providers.Tests/LyricsX.Providers.Tests.csproj
dotnet add src/LyricsX.Providers/LyricsX.Providers.csproj reference src/LyricsX.Core/LyricsX.Core.csproj
dotnet add tests/LyricsX.Core.Tests/LyricsX.Core.Tests.csproj reference src/LyricsX.Core/LyricsX.Core.csproj
dotnet add tests/LyricsX.Providers.Tests/LyricsX.Providers.Tests.csproj reference src/LyricsX.Providers/LyricsX.Providers.csproj
```

- [ ] Replace `Windows/Directory.Build.props` with:

```xml
<Project>
  <PropertyGroup>
    <TargetFramework>net10.0-windows10.0.22000.0</TargetFramework>
    <RuntimeIdentifier>win-x64</RuntimeIdentifier>
    <PlatformTarget>x64</PlatformTarget>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <LangVersion>14.0</LangVersion>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <Deterministic>true</Deterministic>
    <ContinuousIntegrationBuild Condition="'$(CI)' == 'true'">true</ContinuousIntegrationBuild>
    <ManagePackageVersionsCentrally>true</ManagePackageVersionsCentrally>
  </PropertyGroup>
</Project>
```

- [ ] Replace `Windows/Directory.Packages.props` with:

```xml
<Project>
  <ItemGroup>
    <PackageVersion Include="coverlet.collector" Version="10.0.1" />
    <PackageVersion Include="Microsoft.NET.Test.Sdk" Version="18.8.1" />
    <PackageVersion Include="MSTest.TestAdapter" Version="4.3.2" />
    <PackageVersion Include="MSTest.TestFramework" Version="4.3.2" />
    <PackageVersion Include="OpenccNetLib" Version="1.6.1" />
  </ItemGroup>
</Project>
```

- [ ] Make both test project files use the pinned packages:

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <IsPackable>false</IsPackable>
    <IsTestProject>true</IsTestProject>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="coverlet.collector" PrivateAssets="all" />
    <PackageReference Include="Microsoft.NET.Test.Sdk" />
    <PackageReference Include="MSTest.TestAdapter" />
    <PackageReference Include="MSTest.TestFramework" />
  </ItemGroup>
</Project>
```

- [ ] Add `<PackageReference Include="OpenccNetLib" />` to `LyricsX.Core.csproj`, preserve the generated `Project` root, and delete every generated `Class1.cs` and `UnitTest1.cs`.

- [ ] Run:

```powershell
dotnet restore LyricsX.Windows.slnx
dotnet build LyricsX.Windows.slnx --no-restore
```

Expected: restore and build succeed with zero warnings.

- [ ] Commit:

```powershell
git add Windows/LyricsX.Windows.slnx Windows/Directory.Build.props Windows/Directory.Packages.props Windows/src Windows/tests
git commit -m "build(windows): scaffold core and provider projects"
```

## Task 2: Implement domain models, normalization, and fingerprints

**Files:**

- Create: `Windows/src/LyricsX.Core/Domain/MediaModels.cs`
- Create: `Windows/src/LyricsX.Core/Domain/LyricModels.cs`
- Create: `Windows/src/LyricsX.Core/Matching/TrackTextNormalizer.cs`
- Create: `Windows/src/LyricsX.Core/Matching/TrackFingerprint.cs`
- Create: `Windows/tests/LyricsX.Core.Tests/Matching/TrackFingerprintTests.cs`

**Interfaces produced:** the stable contracts above plus `TrackFingerprint.Create`.

- [ ] Write the failing tests:

```csharp
using LyricsX.Core;
using LyricsX.Core.Matching;

namespace LyricsX.Core.Tests.Matching;

[TestClass]
public sealed class TrackFingerprintTests
{
    [TestMethod]
    public void Create_RemovesFeaturedArtistAndEditionNoise()
    {
        var track = new MediaTrack(
            "AppleMusic", "1", "Song（Live） feat. Guest", "Artist & Guest",
            "Album", TimeSpan.FromSeconds(201));

        Assert.AreEqual("song|artist|201", TrackFingerprint.Create(track));
    }

    [TestMethod]
    public void Create_NormalizesFullWidthAndWhitespace()
    {
        var track = new MediaTrack(
            "Browser", "2", " Ｈｅｌｌｏ　World ", " Test  Artist ",
            null, null);

        Assert.AreEqual("hello world|test artist|?", TrackFingerprint.Create(track));
    }

    [TestMethod]
    public void CreateAlbum_UsesPrimaryArtistAndAlbum()
    {
        var track = new MediaTrack(
            "QQMusic", "3", "Song", "Artist feat. Guest",
            "Album（Deluxe）", null);

        Assert.AreEqual("artist|album", TrackFingerprint.CreateAlbum(track));
    }
}
```

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Core.Tests/LyricsX.Core.Tests.csproj --filter TrackFingerprintTests
```

Expected: compilation fails because `TrackFingerprint` does not exist.

- [ ] Implement `MediaModels.cs` and `LyricModels.cs` exactly as shown in **Stable Cross-Plan Contracts**, with `namespace LyricsX.Core;`.

- [ ] Implement `TrackTextNormalizer.cs`:

```csharp
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace LyricsX.Core.Matching;

public static partial class TrackTextNormalizer
{
    public static string Normalize(string value)
    {
        var normalized = value.Normalize(NormalizationForm.FormKC).ToLowerInvariant();
        normalized = EditionNoise().Replace(normalized, " ");
        normalized = FeaturedArtist().Replace(normalized, " ");
        normalized = Separators().Replace(normalized, " ");
        return Whitespace().Replace(normalized, " ").Trim();
    }

    [GeneratedRegex(@"[\(\[（【].*?(live|remaster(ed)?|version|edit|deluxe|豪华版|伴奏|现场).*?[\)\]）】]",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex EditionNoise();

    [GeneratedRegex(@"\s+(feat\.?|ft\.?|featuring)\s+.+$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex FeaturedArtist();

    [GeneratedRegex(@"[‐‑‒–—―·•・/\\|]+")]
    private static partial Regex Separators();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();
}
```

- [ ] Implement `TrackFingerprint.cs`:

```csharp
namespace LyricsX.Core.Matching;

public static class TrackFingerprint
{
    public static string Create(MediaTrack track)
    {
        var title = TrackTextNormalizer.Normalize(track.Title);
        var artist = TrackTextNormalizer.Normalize(PrimaryArtist(track.Artist));
        var seconds = track.Duration is null
            ? "?"
            : Math.Round(track.Duration.Value.TotalSeconds).ToString(
                System.Globalization.CultureInfo.InvariantCulture);
        return $"{title}|{artist}|{seconds}";
    }

    public static string? CreateAlbum(MediaTrack track)
    {
        if (string.IsNullOrWhiteSpace(track.Album))
        {
            return null;
        }

        var artist = TrackTextNormalizer.Normalize(PrimaryArtist(track.Artist));
        var album = TrackTextNormalizer.Normalize(track.Album);
        return $"{artist}|{album}";
    }

    private static string PrimaryArtist(string artist)
    {
        var separators = new[] { " feat. ", " ft. ", " featuring ", " & ", "、", "," };
        var index = separators
            .Select(separator => artist.IndexOf(separator, StringComparison.OrdinalIgnoreCase))
            .Where(value => value >= 0)
            .DefaultIfEmpty(artist.Length)
            .Min();
        return artist[..index];
    }
}
```

- [ ] Run the full core test project:

```powershell
dotnet test tests/LyricsX.Core.Tests/LyricsX.Core.Tests.csproj
```

Expected: all tests pass.

- [ ] Commit:

```powershell
git add Windows/src/LyricsX.Core/Domain Windows/src/LyricsX.Core/Matching Windows/tests/LyricsX.Core.Tests/Matching
git commit -m "feat(core): add lyric domain and track fingerprints"
```

## Task 3: Parse and serialize LRC/LRCX documents

**Files:**

- Create: `Windows/src/LyricsX.Core/Parsing/ILyricParser.cs`
- Create: `Windows/src/LyricsX.Core/Parsing/LrcParser.cs`
- Create: `Windows/src/LyricsX.Core/Parsing/LrcSerializer.cs`
- Create: `Windows/tests/LyricsX.Core.Tests/Parsing/LrcParserTests.cs`

**Interfaces produced:**

```csharp
public interface ILyricParser
{
    LyricDocument Parse(string content);
}
```

- [ ] Write failing tests that cover metadata, multiple timestamps, enhanced-word duration, global offset, and round-trip:

```csharp
using LyricsX.Core.Parsing;

namespace LyricsX.Core.Tests.Parsing;

[TestClass]
public sealed class LrcParserTests
{
    private const string Sample =
        "[ti:Window Song]\n[ar:Codex]\n[offset:250]\n" +
        "[00:01.00][00:03.00]Hello\n[00:05.20]<00:05.20>Win<00:05.60>dows";

    [TestMethod]
    public void Parse_ReadsMetadataOffsetAndRepeatedLines()
    {
        var document = new LrcParser().Parse(Sample);

        Assert.AreEqual("Window Song", document.Metadata.Title);
        Assert.AreEqual(TimeSpan.FromMilliseconds(250), document.Delay);
        Assert.AreEqual(3, document.Lines.Count);
        Assert.AreEqual(TimeSpan.FromSeconds(3), document.Lines[1].Position);
        Assert.AreEqual("Windows", document.Lines[2].Content);
        Assert.AreEqual(2, document.Lines[2].Words?.Count);
        Assert.AreEqual(TimeSpan.FromMilliseconds(400),
            document.Lines[2].Words?[0].Duration);
    }

    [TestMethod]
    public void Serialize_RoundTripsLinePositions()
    {
        var parser = new LrcParser();
        var first = parser.Parse(Sample);
        var second = parser.Parse(new LrcSerializer().Serialize(first));

        CollectionAssert.AreEqual(
            first.Lines.Select(line => line.Position).ToArray(),
            second.Lines.Select(line => line.Position).ToArray());
        CollectionAssert.AreEqual(
            first.Lines[2].Words?.Select(word => word.Position).ToArray(),
            second.Lines[2].Words?.Select(word => word.Position).ToArray());
    }
}
```

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Core.Tests/LyricsX.Core.Tests.csproj --filter LrcParserTests
```

Expected: compilation fails because the parser types do not exist.

- [ ] Implement `ILyricParser.cs`:

```csharp
namespace LyricsX.Core.Parsing;

public interface ILyricParser
{
    LyricDocument Parse(string content);
}
```

- [ ] Implement `LrcParser` with these concrete rules:

  - Normalize CRLF and CR to LF.
  - Parse `ti`, `ar`, `al`, `by`, `length`, and `offset`.
  - Accept `[mm:ss]`, `[mm:ss.xx]`, `[mm:ss.xxx]`, and repeated timestamps.
  - Parse enhanced `<mm:ss.xx>` word tags into `LyricWord` values while also retaining concatenated visible text.
  - Infer each enhanced word duration from the next word timestamp, or from the line end for the final word.
  - Sort lines by time, preserve same-time input order, and infer each line duration from the next distinct timestamp.
  - Ignore malformed metadata and timestamps without throwing.
  - Throw `FormatException("The lyric document contains no timed lines.")` only when no timed line exists.

Use this timestamp helper in the implementation:

```csharp
private static bool TryParseTimestamp(string value, out TimeSpan result)
{
    result = default;
    var parts = value.Split(':', 2);
    if (parts.Length != 2 ||
        !int.TryParse(parts[0], out var minutes) ||
        !decimal.TryParse(parts[1], NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture, out var seconds))
    {
        return false;
    }

    result = TimeSpan.FromMinutes(minutes) + TimeSpan.FromSeconds((double)seconds);
    return result >= TimeSpan.Zero;
}
```

- [ ] Implement `LrcSerializer.Serialize(LyricDocument)` using UTF-8 text, metadata order `ti/ar/al/by/offset`, one line timestamp, two fraction digits, and the invariant timestamp formatter. Emit enhanced word timestamps when `LyricLine.Words` is non-empty so LRCX data round-trips; otherwise emit standard LRC text:

```csharp
private static string FormatTimestamp(TimeSpan value)
{
    var totalMinutes = (int)value.TotalMinutes;
    return FormattableString.Invariant(
        $"{totalMinutes:00}:{value.Seconds:00}.{value.Milliseconds / 10:00}");
}
```

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Core.Tests/LyricsX.Core.Tests.csproj
```

Expected: all parser and fingerprint tests pass.

- [ ] Commit:

```powershell
git add Windows/src/LyricsX.Core/Parsing Windows/tests/LyricsX.Core.Tests/Parsing
git commit -m "feat(core): parse and serialize synchronized lyrics"
```

## Task 4: Score candidates and coordinate first-result plus quality-window search

**Files:**

- Create: `Windows/src/LyricsX.Core/Matching/LyricCandidateScorer.cs`
- Create: `Windows/src/LyricsX.Core/Search/ILyricsSearchCoordinator.cs`
- Create: `Windows/src/LyricsX.Core/Search/LyricsSearchCoordinator.cs`
- Create: `Windows/tests/LyricsX.Core.Tests/Search/LyricsSearchCoordinatorTests.cs`

**Interfaces produced:**

```csharp
namespace LyricsX.Core.Search;

public sealed record LyricSearchState(
    LyricCandidate Candidate,
    bool IsFinal,
    double Score);

public interface ILyricsSearchCoordinator
{
    IAsyncEnumerable<LyricSearchState> SearchAsync(
        LyricSearchRequest request,
        CancellationToken cancellationToken);
}
```

- [ ] Write a failing coordinator test using this complete provider double:

```csharp
using System.Runtime.CompilerServices;
using LyricsX.Core.Search;

namespace LyricsX.Core.Tests.Search;

[TestClass]
public sealed class LyricsSearchCoordinatorTests
{
    [TestMethod]
    public async Task SearchAsync_YieldsFirstCandidateThenBetterCandidateAsFinal()
    {
        var track = new MediaTrack(
            "Browser", "id", "Exact Song", "Exact Artist", "Exact Album",
            TimeSpan.FromSeconds(200));
        var weak = Candidate("weak", "Exact Song", "Other", 0);
        var exact = Candidate("exact", "Exact Song", "Exact Artist", 1);
        var coordinator = new LyricsSearchCoordinator(
            [new FakeProvider("p", weak, exact)],
            TimeSpan.FromMilliseconds(50));

        var results = new List<LyricSearchState>();
        await foreach (var state in coordinator.SearchAsync(
            new LyricSearchRequest(track), CancellationToken.None))
        {
            results.Add(state);
        }

        Assert.AreEqual(2, results.Count);
        Assert.AreSame(weak, results[0].Candidate);
        Assert.IsFalse(results[0].IsFinal);
        Assert.AreSame(exact, results[1].Candidate);
        Assert.IsTrue(results[1].IsFinal);
    }

    private static LyricCandidate Candidate(
        string id, string title, string artist, int rank) =>
        new(id,
            new LyricDocument(
                [new LyricLine(TimeSpan.Zero, null, "line")],
                new LyricMetadata(title, artist, null, null, null,
                    new Dictionary<string, string>()),
                TimeSpan.Zero),
            title, artist, null, TimeSpan.FromSeconds(200), rank);

    private sealed class FakeProvider(
        string id,
        params LyricCandidate[] candidates) : ILyricsProvider
    {
        public string Id => id;

        public async IAsyncEnumerable<LyricCandidate> SearchAsync(
            LyricSearchRequest request,
            [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            foreach (var candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                yield return candidate;
                await Task.Yield();
            }
        }
    }
}
```

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Core.Tests/LyricsX.Core.Tests.csproj --filter LyricsSearchCoordinatorTests
```

Expected: compilation fails because the search types do not exist.

- [ ] Implement `LyricCandidateScorer.Score(MediaTrack, LyricCandidate)` as a value from 0–100:

  - exact normalized title: 40; title containment: 20.
  - exact normalized artist: 30; primary-artist equality: 20.
  - exact normalized album when both exist: 15.
  - duration within 2 seconds: 15; within 5 seconds: 8.
  - subtract `ProviderRank`, clamp to 0–100.
  - reject with score 0 when normalized titles do not match or contain one another.

- [ ] Implement `LyricsSearchCoordinator` with constructor:

```csharp
public LyricsSearchCoordinator(
    IReadOnlyList<ILyricsProvider> providers,
    TimeSpan qualityWindow,
    int retryCount = 2,
    TimeSpan? retryDelay = null)
```

Its observable behavior must be:

  - Start enabled providers concurrently and merge their async streams through a bounded `Channel<LyricCandidate>`.
  - Publish the first score-positive result immediately with `IsFinal = false`.
  - Keep searching for five seconds in production and replace only when score increases.
  - Yield one final state with `IsFinal = true`.
  - Retry a provider twice after a two-second delay on `HttpRequestException` or `TaskCanceledException` not caused by caller cancellation.
  - Cancel all provider enumeration when the track changes or the caller cancels.
  - Complete without a result when every provider returns no score-positive candidate.

- [ ] Add tests for provider concurrency, retry count, cancellation, empty results, duration scoring, and strict-title rejection. Inject a 20–50 ms quality window and zero retry delay in tests.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Core.Tests/LyricsX.Core.Tests.csproj
```

Expected: all tests pass without timing-dependent failures in five consecutive runs.

- [ ] Commit:

```powershell
git add Windows/src/LyricsX.Core/Matching/LyricCandidateScorer.cs Windows/src/LyricsX.Core/Search Windows/tests/LyricsX.Core.Tests/Search
git commit -m "feat(core): coordinate ranked lyric searches"
```

## Task 5: Synchronize playback, apply offsets, filters, and Chinese conversion

**Files:**

- Create: `Windows/src/LyricsX.Core/Playback/LyricSynchronizer.cs`
- Create: `Windows/src/LyricsX.Core/Transforms/LyricLanguageDetector.cs`
- Create: `Windows/src/LyricsX.Core/Transforms/LyricTransformer.cs`
- Create: `Windows/tests/LyricsX.Core.Tests/Playback/LyricSynchronizerTests.cs`
- Create: `Windows/tests/LyricsX.Core.Tests/Transforms/LyricLanguageDetectorTests.cs`
- Create: `Windows/tests/LyricsX.Core.Tests/Transforms/LyricTransformerTests.cs`

**Interfaces produced:**

```csharp
public sealed record LyricFrame(
    int CurrentIndex,
    LyricLine? Current,
    LyricLine? Next,
    double Progress,
    int CurrentWordIndex,
    double WordProgress);
```

- [ ] Write failing synchronization tests:

```csharp
using LyricsX.Core.Playback;

namespace LyricsX.Core.Tests.Playback;

[TestClass]
public sealed class LyricSynchronizerTests
{
    [TestMethod]
    public void GetFrame_AppliesDocumentAndUserOffsets()
    {
        var document = new LyricDocument(
            [
                new LyricLine(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), "one"),
                new LyricLine(TimeSpan.FromSeconds(3), null, "two")
            ],
            new LyricMetadata(null, null, null, null, null,
                new Dictionary<string, string>()),
            TimeSpan.FromMilliseconds(250));

        var frame = new LyricSynchronizer().GetFrame(
            document,
            TimeSpan.FromSeconds(2.75),
            TimeSpan.FromMilliseconds(-500));

        Assert.AreEqual("two", frame.Current?.Content);
        Assert.AreEqual(1, frame.CurrentIndex);
    }
}
```

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Core.Tests/LyricsX.Core.Tests.csproj --filter LyricSynchronizerTests
```

Expected: compilation fails because `LyricSynchronizer` does not exist.

- [ ] Implement binary-search selection in `LyricSynchronizer.GetFrame`; calculate effective time as `playbackPosition + document.Delay + userOffset`, clamp line and word progress to 0–1, use next-line position when the current line has no duration, binary-search enhanced words when present, and return line/word index `-1` before the relevant first timestamp.

- [ ] Implement language detection with:

```csharp
namespace LyricsX.Core.Transforms;

public enum LyricLanguage
{
    Unknown,
    Latin,
    SimplifiedChinese,
    TraditionalChinese,
    Japanese,
    Korean,
    Mixed
}

public sealed class LyricLanguageDetector
{
    public LyricLanguage Detect(LyricDocument document)
    {
        var text = string.Concat(document.Lines.Select(line => line.Content));
        var hasJapanese = text.Any(character => character is >= '\u3040' and <= '\u30ff');
        var hasKorean = text.Any(character => character is >= '\uac00' and <= '\ud7af');
        var hasCjk = text.Any(character => character is >= '\u3400' and <= '\u9fff');
        var hasLatin = text.Any(char.IsAsciiLetter);

        if (hasJapanese) return LyricLanguage.Japanese;
        if (hasKorean) return LyricLanguage.Korean;
        if (hasCjk && hasLatin) return LyricLanguage.Mixed;
        if (hasCjk)
        {
            return Opencc.ZhoCheck(text) switch
            {
                1 => LyricLanguage.TraditionalChinese,
                2 => LyricLanguage.SimplifiedChinese,
                _ => LyricLanguage.Mixed
            };
        }

        return hasLatin ? LyricLanguage.Latin : LyricLanguage.Unknown;
    }
}
```

- [ ] Implement `LyricTransformer`:

```csharp
using OpenccNetLib;

namespace LyricsX.Core.Transforms;

public enum ChineseConversion { None, Simplified, Traditional }

public sealed record LyricTransformOptions(
    ChineseConversion ChineseConversion,
    bool RemoveEmptyLines,
    IReadOnlyList<string> BlockedPhrases);

public sealed class LyricTransformer
{
    public LyricDocument Apply(LyricDocument document, LyricTransformOptions options)
    {
        Opencc? converter = options.ChineseConversion switch
        {
            ChineseConversion.Simplified => new Opencc(OpenccConfig.T2S),
            ChineseConversion.Traditional => new Opencc(OpenccConfig.S2T),
            _ => null
        };

        var lines = document.Lines
            .Where(line => !options.RemoveEmptyLines ||
                !string.IsNullOrWhiteSpace(line.Content))
            .Where(line => !options.BlockedPhrases.Any(phrase =>
                line.Content.Contains(phrase, StringComparison.OrdinalIgnoreCase)))
            .Select(line => line with
            {
                Content = converter?.Convert(line.Content) ?? line.Content
            })
            .ToArray();

        return document with { Lines = lines };
    }
}
```

- [ ] Test playback before/inside/after lyrics, pause stability, document offset, user offset, enhanced-word selection/progress, empty-line removal, blocked phrases, Latin/Japanese/Korean/simplified/traditional/mixed recognition, and `汉字` to Traditional conversion.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Core.Tests/LyricsX.Core.Tests.csproj
```

Expected: all tests pass.

- [ ] Commit:

```powershell
git add Windows/src/LyricsX.Core/Playback Windows/src/LyricsX.Core/Transforms Windows/tests/LyricsX.Core.Tests/Playback Windows/tests/LyricsX.Core.Tests/Transforms
git commit -m "feat(core): synchronize and transform lyrics"
```

## Task 6: Persist settings, cache, and manual track associations

**Files:**

- Create: `Windows/src/LyricsX.Core/Storage/AppSettings.cs`
- Create: `Windows/src/LyricsX.Core/Storage/JsonFileStore.cs`
- Create: `Windows/src/LyricsX.Core/Storage/LyricCache.cs`
- Create: `Windows/src/LyricsX.Core/Storage/ManualAssociationStore.cs`
- Create: `Windows/tests/LyricsX.Core.Tests/Storage/JsonStorageTests.cs`

**Interfaces produced:**

```csharp
public sealed record ProviderSetting(string Id, bool Enabled, int Priority);

public sealed record AppSettings(
    int SchemaVersion,
    bool LaunchAtLogin,
    bool ShowWhenPaused,
    int WidthDip,
    int UserOffsetMilliseconds,
    ChineseConversion ChineseConversion,
    IReadOnlyList<ProviderSetting> Providers,
    IReadOnlyList<string> IgnoredTrackFingerprints,
    IReadOnlyList<string> IgnoredAlbumFingerprints);

public interface ISettingsStore
{
    Task<AppSettings> LoadAsync(CancellationToken cancellationToken);
    Task SaveAsync(AppSettings settings, CancellationToken cancellationToken);
}
```

- [ ] Write failing tests with a test-owned temporary directory. Cover missing files, atomic replacement, invalid JSON backup, cache round-trip, and a manual association resolving the same normalized fingerprint.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Core.Tests/LyricsX.Core.Tests.csproj --filter "FullyQualifiedName~Storage"
```

Expected: compilation fails because storage types do not exist.

- [ ] Implement these paths under injected root `%LOCALAPPDATA%\LyricsX`:

```text
settings.json
associations.json
cache/<sha256 fingerprint>.lrc
cache/<sha256 fingerprint>.json
logs/
backups/
```

- [ ] Implement `JsonFileStore<T>` with `System.Text.Json`, camel-case property names, enum strings, UTF-8 without BOM, `FileOptions.WriteThrough`, a same-directory `.tmp` file, and `File.Move(temp, target, true)`. On malformed JSON, move the source to `backups/<name>-<UTC timestamp>.invalid.json` and return the provided default factory.

- [ ] Set defaults to width 260 DIPs, offset 0, show while paused, auto-start off, conversion none, empty ignored-track/album lists, and provider order QQMusic, NetEase, Kugou, Musixmatch, LRCLIB, Spotify with Spotify disabled.

- [ ] Store ignored tracks by `TrackFingerprint.Create(track)` and ignored albums by `TrackFingerprint.CreateAlbum(track)`, where album fingerprints contain normalized primary artist and album only. Treat a missing album as non-ignorable and cover both lists with round-trip tests.

- [ ] Make `LyricCache` write the LRC document and a JSON sidecar containing provider, original query, score, retrieved UTC time, and normalized fingerprint.

- [ ] Make `ManualAssociationStore` map fingerprint to cache key and expose `TryResolveAsync`, `SetAsync`, and `RemoveAsync`.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Core.Tests/LyricsX.Core.Tests.csproj
```

Expected: all core tests pass.

- [ ] Commit:

```powershell
git add Windows/src/LyricsX.Core/Storage Windows/tests/LyricsX.Core.Tests/Storage
git commit -m "feat(core): persist settings cache and associations"
```

## Task 7: Add provider HTTP infrastructure and response fixtures

**Files:**

- Create: `Windows/src/LyricsX.Providers/ProviderHttpClient.cs`
- Create: `Windows/src/LyricsX.Providers/ProviderJson.cs`
- Create: `Windows/tests/LyricsX.Providers.Tests/Infrastructure/StubHttpMessageHandler.cs`
- Create: `Windows/tests/LyricsX.Providers.Tests/Fixtures/*.json`
- Create: `Windows/tests/LyricsX.Providers.Tests/Infrastructure/ProviderHttpClientTests.cs`

**Interfaces produced:** a provider-safe HTTP layer with throttling and redacted diagnostics.

- [ ] Write the complete HTTP test double:

```csharp
namespace LyricsX.Providers.Tests.Infrastructure;

internal sealed class StubHttpMessageHandler(
    Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
{
    public List<HttpRequestMessage> Requests { get; } = [];

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        Requests.Add(request);
        return Task.FromResult(responder(request));
    }
}
```

- [ ] Write failing tests proving a 15-second timeout, `LyricsX-Windows/<version>` user agent, gzip/deflate support, non-success `HttpRequestException`, and cancellation propagation.

- [ ] Implement one named `HttpClient` per provider through `IHttpClientFactory` in plan 03. `ProviderHttpClient` must apply a per-provider `SemaphoreSlim(2)`, set `Accept-Language: zh-CN,zh;q=0.9,en;q=0.5`, read at most 4 MiB, and deserialize with:

```csharp
internal static readonly JsonSerializerOptions Options = new()
{
    PropertyNameCaseInsensitive = true,
    NumberHandling = JsonNumberHandling.AllowReadingFromString
};
```

- [ ] Add sanitized fixtures for success, no-result, malformed-result, and HTTP-error cases. Each fixture must be under 30 KiB and must not contain tokens, cookies, or personal account identifiers.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Providers.Tests/LyricsX.Providers.Tests.csproj
```

Expected: all infrastructure tests pass.

- [ ] Commit:

```powershell
git add Windows/src/LyricsX.Providers/ProviderHttpClient.cs Windows/src/LyricsX.Providers/ProviderJson.cs Windows/tests/LyricsX.Providers.Tests/Infrastructure Windows/tests/LyricsX.Providers.Tests/Fixtures
git commit -m "feat(providers): add safe provider HTTP infrastructure"
```

## Task 8: Port LRCLIB, NetEase, QQMusic, and Kugou providers

**Files:**

- Create: `Windows/src/LyricsX.Providers/Lrclib/LrclibProvider.cs`
- Create: `Windows/src/LyricsX.Providers/NetEase/NetEaseProvider.cs`
- Create: `Windows/src/LyricsX.Providers/QQMusic/QQMusicProvider.cs`
- Create: `Windows/src/LyricsX.Providers/Kugou/KugouProvider.cs`
- Create: `Windows/tests/LyricsX.Providers.Tests/Lrclib/LrclibProviderTests.cs`
- Create: `Windows/tests/LyricsX.Providers.Tests/NetEase/NetEaseProviderTests.cs`
- Create: `Windows/tests/LyricsX.Providers.Tests/QQMusic/QQMusicProviderTests.cs`
- Create: `Windows/tests/LyricsX.Providers.Tests/Kugou/KugouProviderTests.cs`

**Interfaces consumed:** `ILyricsProvider`, `LrcParser`, `ProviderHttpClient`.

- [ ] For each provider, first write fixture-driven tests asserting:

  - query parameters are UTF-8 encoded;
  - the provider returns no candidate for an empty or instrumental lyric;
  - the candidate includes title, artist, album, duration, and provider rank;
  - malformed remote rows are skipped without aborting valid rows;
  - cancellation reaches `HttpClient`.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Providers.Tests/LyricsX.Providers.Tests.csproj --filter "FullyQualifiedName~Lrclib|FullyQualifiedName~NetEase|FullyQualifiedName~QQMusic|FullyQualifiedName~Kugou"
```

Expected: compilation fails because the provider classes do not exist.

- [ ] Implement each provider against the same public endpoints and request shapes used by the checked-in LyricsKit implementation:

  - Read the exact current request mapping before coding from `DerivedData/SourcePackages/checkouts/LyricsKit/Sources/LyricsService/Provider/Services/`.
  - Preserve provider attribution and any required non-secret headers.
  - Do not copy Swift networking code mechanically; map decoded fields into the stable C# contracts.
  - Use `ProviderRank` values matching the settings order: QQMusic 0, NetEase 1, Kugou 2, LRCLIB 4.
  - Parse only synchronized lyrics; skip plain-text-only responses in the first release.

- [ ] Add a live smoke-test category that is excluded from normal runs:

```csharp
[TestCategory("LiveProvider")]
[TestMethod]
public async Task SearchAsync_ReturnsWithoutProtocolFailure()
{
    using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var count = 0;
    await foreach (var candidate in _provider
        .SearchAsync(_request, timeout.Token)
        .WithCancellation(timeout.Token))
    {
        Assert.IsFalse(string.IsNullOrWhiteSpace(candidate.ProviderId));
        count++;
        break;
    }

    Assert.IsTrue(count is 0 or 1);
}
```

- [ ] Run offline tests:

```powershell
dotnet test tests/LyricsX.Providers.Tests/LyricsX.Providers.Tests.csproj --filter "TestCategory!=LiveProvider"
```

Expected: all fixture tests pass.

- [ ] On a network-enabled Windows machine, run:

```powershell
dotnet test tests/LyricsX.Providers.Tests/LyricsX.Providers.Tests.csproj --filter "TestCategory=LiveProvider"
```

Expected: every provider completes without authentication, TLS, or response-shape exceptions; zero results are allowed.

- [ ] Commit:

```powershell
git add Windows/src/LyricsX.Providers/Lrclib Windows/src/LyricsX.Providers/NetEase Windows/src/LyricsX.Providers/QQMusic Windows/src/LyricsX.Providers/Kugou Windows/tests/LyricsX.Providers.Tests
git commit -m "feat(providers): add four public lyric sources"
```

## Task 9: Add Musixmatch and optional Spotify authorization

**Files:**

- Create: `Windows/src/LyricsX.Providers/Musixmatch/MusixmatchProvider.cs`
- Create: `Windows/src/LyricsX.Providers/Spotify/SpotifyLyricsProvider.cs`
- Create: `Windows/src/LyricsX.Providers/Spotify/ISpotifyTokenStore.cs`
- Create: `Windows/src/LyricsX.Providers/Spotify/SpotifyAuthorizationService.cs`
- Create: `Windows/tests/LyricsX.Providers.Tests/Musixmatch/MusixmatchProviderTests.cs`
- Create: `Windows/tests/LyricsX.Providers.Tests/Spotify/SpotifyProviderTests.cs`

**Interfaces produced:**

```csharp
public interface ISpotifyTokenStore
{
    Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken);
    Task SaveRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken);
    Task ClearAsync(CancellationToken cancellationToken);
}
```

- [ ] Write failing fixture tests for Musixmatch result mapping and Spotify states: disabled, missing client ID, authorization required, expired access token refreshed once, 401 after refresh, and token redaction.

- [ ] Run:

```powershell
dotnet test tests/LyricsX.Providers.Tests/LyricsX.Providers.Tests.csproj --filter "FullyQualifiedName~Musixmatch|FullyQualifiedName~Spotify"
```

Expected: compilation fails because the provider classes do not exist.

- [ ] Implement Musixmatch using only the request flow already present in LyricsKit. Give it provider rank 3. Fail closed with a provider-specific diagnostic when the upstream protocol changes.

- [ ] Implement Spotify Authorization Code with PKCE:

  - Generate a 32-byte random verifier and SHA-256 URL-safe challenge.
  - Listen only on `http://127.0.0.1:<ephemeral-port>/callback/`.
  - Include a cryptographically random state and compare with fixed-time equality.
  - Exchange the code, persist only the refresh token through `ISpotifyTokenStore`, and keep access tokens in memory.
  - Never embed a client secret in the desktop app.
  - Keep provider rank 5 and `Enabled = false` by default.

- [ ] The plan 02 Windows implementation of `ISpotifyTokenStore` must use Windows Credential Manager; tests here use an in-memory store:

```csharp
internal sealed class MemorySpotifyTokenStore : ISpotifyTokenStore
{
    public string? Token { get; private set; }

    public Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken) =>
        Task.FromResult(Token);

    public Task SaveRefreshTokenAsync(
        string refreshToken,
        CancellationToken cancellationToken)
    {
        Token = refreshToken;
        return Task.CompletedTask;
    }

    public Task ClearAsync(CancellationToken cancellationToken)
    {
        Token = null;
        return Task.CompletedTask;
    }
}
```

- [ ] Run:

```powershell
dotnet test LyricsX.Windows.slnx --filter "TestCategory!=LiveProvider"
dotnet build LyricsX.Windows.slnx --configuration Release
```

Expected: all tests pass and the Release build has zero warnings.

- [ ] Commit:

```powershell
git add Windows/src/LyricsX.Providers/Musixmatch Windows/src/LyricsX.Providers/Spotify Windows/tests/LyricsX.Providers.Tests/Musixmatch Windows/tests/LyricsX.Providers.Tests/Spotify
git commit -m "feat(providers): add Musixmatch and optional Spotify"
```

## Plan 01 Exit Criteria

- [ ] `dotnet test Windows/LyricsX.Windows.slnx --filter "TestCategory!=LiveProvider"` passes.
- [ ] Core tests are deterministic and do not access network, registry, WinUI, or user profile data.
- [ ] Provider fixture tests contain no credentials or personal data.
- [ ] The first acceptable candidate is observable before the final five-second quality decision.
- [ ] LRC/LRCX, offset, conversion, filtering, cache, and manual association behaviors match the approved design.
- [ ] Plans 02 and 03 can compile against the stable contracts without referencing provider internals.
