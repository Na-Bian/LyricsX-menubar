using LyricsX.Platform.Windows.Security;

namespace LyricsX.Platform.Windows.Tests.Security;

[TestClass]
public sealed class WindowsCredentialStoreTests
{
    // Credential Manager rejects writes from network (SSH) logon sessions;
    // these tests must run in the interactive session (plan tasks on the VM).
    [TestCategory("InteractiveSession")]
    [TestMethod]
    public async Task SaveLoadDelete_RoundTripsSecret()
    {
        var store = new WindowsCredentialStore(targetPrefix: "LyricsX/Test");
        var secret = "refresh-token-秘密-123";

        await store.SaveAsync("Credential", secret, CancellationToken.None);
        var loaded = await store.LoadAsync("Credential", CancellationToken.None);

        Assert.AreEqual(secret, loaded);

        await store.DeleteAsync("Credential", CancellationToken.None);
        Assert.IsNull(await store.LoadAsync("Credential", CancellationToken.None));
    }

    [TestCategory("InteractiveSession")]
    [TestMethod]
    public async Task Load_MissingCredential_ReturnsNull()
    {
        var store = new WindowsCredentialStore(targetPrefix: "LyricsX/Test");

        Assert.IsNull(await store.LoadAsync("Missing", CancellationToken.None));
    }

    [TestCategory("InteractiveSession")]
    [TestMethod]
    public async Task Save_OverwritesExistingCredential()
    {
        var store = new WindowsCredentialStore(targetPrefix: "LyricsX/Test");
        try
        {
            await store.SaveAsync("Credential", "first", CancellationToken.None);
            await store.SaveAsync("Credential", "second", CancellationToken.None);

            Assert.AreEqual("second",
                await store.LoadAsync("Credential", CancellationToken.None));
        }
        finally
        {
            await store.DeleteAsync("Credential", CancellationToken.None);
        }
    }

    [TestMethod]
    public async Task FullTargetName_Prefixed()
    {
        var store = new WindowsCredentialStore(targetPrefix: "LyricsX/Spotify");

        Assert.AreEqual("LyricsX/SpotifyRefreshToken", store.FullTargetName("RefreshToken"));
    }
}
