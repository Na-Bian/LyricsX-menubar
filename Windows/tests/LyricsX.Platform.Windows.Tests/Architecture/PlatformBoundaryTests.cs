namespace LyricsX.Platform.Windows.Tests.Architecture;

[TestClass]
public sealed class PlatformBoundaryTests
{
    [TestMethod]
    public void Core_DoesNotReferenceWindowsPlatform()
    {
        var references = typeof(LyricsX.Core.MediaTrack)
            .Assembly.GetReferencedAssemblies()
            .Select(name => name.Name)
            .ToArray();

        CollectionAssert.DoesNotContain(references, "LyricsX.Platform.Windows");
        CollectionAssert.DoesNotContain(references, "Microsoft.WindowsAppSDK");
        CollectionAssert.DoesNotContain(references, "Microsoft.WinUI");
    }

    [TestMethod]
    public void Providers_DoesNotReferenceWindowsPlatform()
    {
        var references = typeof(LyricsX.Providers.ProviderHttpClient)
            .Assembly.GetReferencedAssemblies()
            .Select(name => name.Name)
            .ToArray();

        CollectionAssert.DoesNotContain(references, "LyricsX.Platform.Windows");
    }
}
