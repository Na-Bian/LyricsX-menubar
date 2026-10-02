using System.IO.Compression;
using System.Text;

namespace LyricsX.Providers;

/// <summary>
/// Kugou KRC cipher: base64 payload starting with the <c>krc1</c> magic,
/// XOR-decrypted with a fixed 16-byte key, two leading bytes dropped, and a
/// raw DEFLATE stream inflated — mirroring the checked-in LyricsKit decrypter.
/// </summary>
public static class KugouKrcCipher
{
    private const int MaxInflatedBytes = 16 * 1024 * 1024;

    private static readonly byte[] Magic = "krc1"u8.ToArray();
    private static readonly byte[] DecodeKey = [64, 71, 97, 119, 94, 50, 116, 71, 81, 54, 49, 45, 206, 210, 110, 105];

    public static string? DecryptBase64(string base64)
    {
        byte[] data;
        try
        {
            data = Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return null;
        }

        if (data.Length < Magic.Length + 4 ||
            !data.AsSpan(0, Magic.Length).SequenceEqual(Magic))
        {
            return null;
        }

        var decrypted = new byte[data.Length - Magic.Length];
        for (var i = 0; i < decrypted.Length; i++)
        {
            decrypted[i] = (byte)(data[i + Magic.Length] ^ DecodeKey[i & 0b1111]);
        }

        try
        {
            using var input = new MemoryStream(decrypted, 2, decrypted.Length - 2);
            using var deflate = new DeflateStream(input, CompressionMode.Decompress);
            var text = Encoding.UTF8.GetString(InflateBounded(deflate));
            return text.Length > 0 ? text : null;
        }
        catch (InvalidDataException)
        {
            return null;
        }
    }

    private static byte[] InflateBounded(DeflateStream deflate)
    {
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = deflate.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (output.Length + read > MaxInflatedBytes)
            {
                // decompression bomb: drop the payload instead of trusting it
                return [];
            }

            output.Write(buffer, 0, read);
        }

        return output.ToArray();
    }

    public static string EncryptToBase64(string krc)
    {
        using var deflated = new MemoryStream();
        using (var deflate = new DeflateStream(deflated, CompressionLevel.Optimal, leaveOpen: true))
        {
            deflate.Write(Encoding.UTF8.GetBytes(krc));
        }

        var compressed = deflated.ToArray();
        var encrypted = new byte[Magic.Length + 2 + compressed.Length];
        Magic.CopyTo(encrypted, 0);
        for (var i = 0; i < compressed.Length + 2; i++)
        {
            var payload = i < 2 ? (byte)0 : compressed[i - 2];
            encrypted[Magic.Length + i] = (byte)(payload ^ DecodeKey[i & 0b1111]);
        }

        return Convert.ToBase64String(encrypted);
    }
}
