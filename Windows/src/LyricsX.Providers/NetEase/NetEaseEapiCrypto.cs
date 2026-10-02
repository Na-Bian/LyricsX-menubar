using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace LyricsX.Providers;

/// <summary>
/// NetEase eapi request encryption: AES-128-ECB with a fixed public key over a
/// digest-bound payload, mirroring the flow used by the checked-in LyricsKit
/// implementation.
/// </summary>
public static class NetEaseEapiCrypto
{
    private const string EapiKey = "e82ckenh8dichen8";

    public static string EncryptParams(string eapiUrl, IReadOnlyDictionary<string, string> payload)
    {
        var modifiedUrl = EapiPathOf(eapiUrl);
        var text = JsonSerializer.Serialize(payload);
        var digest = Md5Hex("nobody" + modifiedUrl + "use" + text + "md5forencrypt");
        var data = $"{modifiedUrl}-36cd479b6b5-{text}-36cd479b6b5-{digest}";
        var encrypted = Transform(Encoding.UTF8.GetBytes(data), decrypt: false);
        return Convert.ToHexString(encrypted);
    }

    public static string DecryptParams(string hex) =>
        Encoding.UTF8.GetString(Transform(Convert.FromHexString(hex), decrypt: true));

    public static string EapiPathOf(string eapiUrl) =>
        eapiUrl
            .Replace("https://interface3.music.163.com/e", "/", StringComparison.Ordinal)
            .Replace("https://interface.music.163.com/e", "/", StringComparison.Ordinal);

    private static byte[] Transform(byte[] data, bool decrypt)
    {
        using var aes = Aes.Create();
        aes.Mode = CipherMode.ECB;
        aes.Padding = PaddingMode.PKCS7;
        aes.Key = Encoding.UTF8.GetBytes(EapiKey);
        using var transform = decrypt ? aes.CreateDecryptor() : aes.CreateEncryptor();
        return transform.TransformFinalBlock(data, 0, data.Length);
    }

    private static string Md5Hex(string value) =>
        Convert.ToHexString(MD5.HashData(Encoding.UTF8.GetBytes(value))).ToLowerInvariant();
}
