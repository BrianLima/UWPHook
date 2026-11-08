using System.Security.Cryptography;
using System.Text;
using UWPHook.Core.Configuration;

namespace UWPHook.Infrastructure
{
    /// <summary>
    /// Protects secrets with Windows DPAPI for the current Windows user, as Base64. Only that user,
    /// on this machine, can read them back.
    /// <see cref="ProtectedData"/> ships in the Windows Desktop runtime that WPF already uses; a package
    /// reference to System.Security.Cryptography.ProtectedData would be pruned (NU1510).
    /// </summary>
    internal sealed class DpapiSecretProtector : ISecretProtector
    {
        // Ties the protected text to UWPHook, so another app's DPAPI blob does not decrypt as a key.
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("UWPHook.SteamGridDbApiKey");

        public string Protect(string plain) =>
            Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(plain), Entropy, DataProtectionScope.CurrentUser));

        /// <exception cref="FormatException">Not Base64.</exception>
        /// <exception cref="CryptographicException">Protected for another user or machine, or not DPAPI at all.</exception>
        public string Unprotect(string protectedText) =>
            Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(protectedText), Entropy, DataProtectionScope.CurrentUser));
    }
}
