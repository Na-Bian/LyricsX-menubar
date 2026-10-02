using LyricsX.Providers;

namespace LyricsX.Providers.Tests.Kugou;

[TestClass]
public sealed class KugouKrcCipherTests
{
    [TestMethod]
    public void DecryptBase64_ReturnsNullForDecompressionBomb()
    {
        // 32 MiB of zeros deflates tiny but must not be inflated in full
        var bomb = new string('\0', 32 * 1024 * 1024);
        var encrypted = KugouKrcCipher.EncryptToBase64(bomb);

        Assert.IsNull(KugouKrcCipher.DecryptBase64(encrypted));
    }

    [TestMethod]
    public void EncryptDecrypt_RoundTripsRealisticPayload()
    {
        var krc = "[offset:250]\n[1000,2000]<0,500,0>晴<500,1500,0>天\n";
        var encrypted = KugouKrcCipher.EncryptToBase64(krc);

        Assert.AreEqual(krc, KugouKrcCipher.DecryptBase64(encrypted));
    }
}
