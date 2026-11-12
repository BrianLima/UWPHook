namespace UWPHook.Core.Configuration;

/// <summary>Encrypts a secret (the SteamGridDB API key) before it is written to disk.</summary>
public interface ISecretProtector
{
    string Protect(string plain);

    /// <summary>Throws when the text cannot be decrypted, e.g. it was protected for another Windows user or machine.</summary>
    string Unprotect(string protectedText);
}
