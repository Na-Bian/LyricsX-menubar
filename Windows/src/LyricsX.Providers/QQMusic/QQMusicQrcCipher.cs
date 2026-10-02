using System.Security.Cryptography;
using System.Text;

namespace LyricsX.Providers;

/// <summary>
/// QQMusic QRC content cipher: three single-DES passes over 8-byte blocks
/// with the first 8 bytes of each published 16-byte key string, mirroring the
/// checked-in LyricsKit decrypter (decrypt K1, encrypt K2, decrypt K3).
/// </summary>
public static class QQMusicQrcCipher
{
    private static readonly byte[] Key1 = "!@#)(NHL"u8.ToArray();
    private static readonly byte[] Key2 = "123ZXC!@"u8.ToArray();
    private static readonly byte[] Key3 = "!@#)(*$%"u8.ToArray();

    public static string? DecryptHex(string hex)
    {
        try
        {
            var bytes = Convert.FromHexString(CleanHex(hex));
            var processed = Process(bytes, decryptFirst: true);
            var text = Encoding.UTF8.GetString(processed).TrimEnd('\0');
            return text.Length > 0 ? text : null;
        }
        catch (FormatException)
        {
            return null;
        }
        catch (CryptographicException)
        {
            return null;
        }
    }

    public static string EncryptToHex(string plain)
    {
        var processed = Process(Encoding.UTF8.GetBytes(plain), decryptFirst: false);
        return Convert.ToHexString(processed);
    }

    private static byte[] Process(byte[] data, bool decryptFirst)
    {
        byte[] buffer;
        if (decryptFirst)
        {
            var length = data.Length - data.Length % 8;
            buffer = new byte[length];
            Array.Copy(data, buffer, length);
        }
        else
        {
            // zero-pad the plaintext up to a full DES block so the inverse
            // direction mirrors the server-side encrypted payloads
            var length = (data.Length + 7) / 8 * 8;
            buffer = new byte[length];
            Array.Copy(data, buffer, data.Length);
        }

        // decrypt: K1d -> K2e -> K3d; encrypt is the exact inverse sequence
        return decryptFirst
            ? Des(Des(Des(buffer, Key1, decrypt: true), Key2, decrypt: false),
                Key3, decrypt: true)
            : Des(Des(Des(buffer, Key3, decrypt: false), Key2, decrypt: true),
                Key1, decrypt: false);
    }

    private static byte[] Des(byte[] input, byte[] key, bool decrypt)
    {
        using var des = DES.Create();
        des.Mode = CipherMode.ECB;
        des.Padding = PaddingMode.None;
        des.Key = key;
        using var transform = decrypt ? des.CreateDecryptor() : des.CreateEncryptor();
        return transform.TransformFinalBlock(input, 0, input.Length);
    }

    private static string CleanHex(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var character in value)
        {
            if (!char.IsWhiteSpace(character))
            {
                builder.Append(character);
            }
        }

        return builder.ToString();
    }
}
