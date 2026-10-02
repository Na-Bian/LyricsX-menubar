using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using LyricsX.Platform.Windows.Interop;

namespace LyricsX.Platform.Windows.Security;

/// <summary>
/// ISpotifyTokenStore implementation over Windows Credential Manager
/// (per-user, no elevation). Native credential blobs are zeroed before
/// freeing; secrets never appear in exceptions or logs.
/// </summary>
public sealed class WindowsCredentialStore(string targetPrefix = "LyricsX/Spotify")
    : Providers.ISpotifyTokenStore
{
    public string FullTargetName(string key) => targetPrefix + key;

    public Task SaveAsync(string key, string secret, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            var blob = Encoding.UTF8.GetBytes(secret);
            var target = FullTargetName(key);
            var targetChars = target.ToCharArray();
            var targetBuffer = new LocalAllocBuffer((targetChars.Length + 1) * 2);
            Marshal.Copy(targetChars, 0, targetBuffer.Pointer, targetChars.Length);
            using var blobBuffer = new ZeroableBuffer(Marshal.AllocHGlobal(blob.Length), blob.Length);
            Marshal.Copy(blob, 0, blobBuffer.Pointer, blob.Length);

            var credential = new NativeMethods.CREDENTIAL
            {
                Flags = 0,
                Type = NativeMethods.CRED_TYPE_GENERIC,
                TargetName = targetBuffer.Pointer,
                CredentialBlobSize = (uint)blob.Length,
                CredentialBlob = blobBuffer.Pointer,
                Persist = 2 // CRED_PERSIST_LOCAL_MACHINE: per-user roaming off
            };
            if (!NativeMethods.CredWrite(ref credential, 0))
            {
                throw new Win32Exception(
                    Marshal.GetLastWin32Error(), "CredWrite failed");
            }
        }, cancellationToken);

    public Task<string?> LoadAsync(string key, CancellationToken cancellationToken) =>
        Task.Run<string?>(() =>
        {
            if (!NativeMethods.CredRead(FullTargetName(key), NativeMethods.CRED_TYPE_GENERIC, 0, out var ptr))
            {
                return null;
            }

            try
            {
                var credential = Marshal.PtrToStructure<NativeMethods.CREDENTIAL>(ptr);
                if (credential.CredentialBlobSize == 0 || credential.CredentialBlob == nint.Zero)
                {
                    return null;
                }

                var blob = new byte[credential.CredentialBlobSize];
                Marshal.Copy(credential.CredentialBlob, blob, 0, blob.Length);
                return Encoding.UTF8.GetString(blob);
            }
            finally
            {
                // zero the returned blob copy in native memory before freeing
                var credential = Marshal.PtrToStructure<NativeMethods.CREDENTIAL>(ptr);
                if (credential.CredentialBlob != nint.Zero && credential.CredentialBlobSize > 0)
                {
                    NativeMethods.SecureZeroMemory(
                        credential.CredentialBlob, credential.CredentialBlobSize);
                }

                NativeMethods.CredFree(ptr);
            }
        }, cancellationToken);

    public Task DeleteAsync(string key, CancellationToken cancellationToken) =>
        Task.Run(() =>
        {
            _ = NativeMethods.CredDelete(FullTargetName(key), NativeMethods.CRED_TYPE_GENERIC, 0);
        }, cancellationToken);

    public Task<string?> GetRefreshTokenAsync(CancellationToken cancellationToken) =>
        LoadAsync("RefreshToken", cancellationToken);

    public Task SaveRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken) =>
        SaveAsync("RefreshToken", refreshToken, cancellationToken);

    public Task ClearAsync(CancellationToken cancellationToken) =>
        DeleteAsync("RefreshToken", cancellationToken);
}
