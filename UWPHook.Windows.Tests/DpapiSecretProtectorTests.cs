using UWPHook.Infrastructure;
using Xunit;

namespace UWPHook.Windows.Tests;

// The SteamGridDB key in settings.json is protected with DPAPI for the current Windows user.
public class DpapiSecretProtectorTests
{
    private const string Key = "0123456789abcdef0123456789abcdef";

    private readonly DpapiSecretProtector protector = new();

    [Theory]
    [InlineData(Key)]
    [InlineData("clé 鍵 with spaces")]
    [InlineData("")]
    public void Unprotect_GivesBackWhatWasProtected(string plain) =>
        Assert.Equal(plain, protector.Unprotect(protector.Protect(plain)));

    [Fact]
    public void Protect_DoesNotContainThePlainText()
    {
        var protectedText = protector.Protect(Key);

        Assert.NotEqual(Key, protectedText);
        Assert.DoesNotContain(Key, protectedText);
    }

    [Fact]
    public void Protect_IsBase64() =>
        Assert.NotEmpty(Convert.FromBase64String(protector.Protect(Key)));

    // SettingsStore turns these failures into an empty key (ISecretProtector's contract: Unprotect throws).
    [Theory]
    [InlineData("not base64 !!")]
    [InlineData("AAECAwQFBgcICQ==")]
    public void Unprotect_TextThatIsNotProtected_Throws(string garbage) =>
        Assert.ThrowsAny<Exception>(() => protector.Unprotect(garbage));
}
