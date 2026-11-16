using System.Security.Cryptography;
using Tests.Extensions;

namespace Tests;

public class TokenServiceTestsTest
{
    [Fact]
    public async Task Verify_ReturnsTrue_ForValidToken()
    {
        using RSA rsa = TestableTokenService.CreateRsaKeyPair();

        string token = TestableTokenService.CreateAndSignToken(
            rsa.ExportRSAPrivateKeyPem(),
            "SFTP2GO Master Server",
            "Server A");

        bool result = await PAM_Authenticator.TokenService.Verify(
            token,
            rsa.ExportRSAPublicKeyPem(),
            "Server A");

        Assert.True(result);
    }

    [Fact]
    public async Task Verify_ReturnsFalse_ForInvalidIssuer()
    {
        using RSA rsa = TestableTokenService.CreateRsaKeyPair();

        string token = TestableTokenService.CreateAndSignToken(
            rsa.ExportRSAPrivateKeyPem(),
            "Invalid Issuer",
            "Server A");

        bool result = await PAM_Authenticator.TokenService.Verify(
            token,
            rsa.ExportRSAPublicKeyPem(),
            "Server A");

        Assert.False(result);
    }

    [Fact]
    public async Task Verify_ReturnsFalse_ForInvalidAudience()
    {
        using RSA rsa = TestableTokenService.CreateRsaKeyPair();

        string token = TestableTokenService.CreateAndSignToken(
            rsa.ExportRSAPrivateKeyPem(),
            "SFTP2GO Master Server",
            "Server A");

        bool result = await PAM_Authenticator.TokenService.Verify(
            token,
            rsa.ExportRSAPublicKeyPem(),
            "Server B");

        Assert.False(result);
    }

    [Fact]
    public async Task Verify_ReturnsFalse_WhenSignatureWasCreatedWithDifferentKey()
    {
        using RSA signingRsa = TestableTokenService.CreateRsaKeyPair();
        using RSA validationRsa = TestableTokenService.CreateRsaKeyPair();

        string token = TestableTokenService.CreateAndSignToken(
            signingRsa.ExportRSAPrivateKeyPem(),
            "SFTP2GO Master Server",
            "Server A");

        bool result = await PAM_Authenticator.TokenService.Verify(
            token,
            validationRsa.ExportRSAPublicKeyPem(),
            "Server A");

        Assert.False(result);
    }

    [Fact]
    public async Task Verify_ReturnsFalse_ForMalformedToken()
    {
        using RSA rsa = TestableTokenService.CreateRsaKeyPair();

        bool result = await PAM_Authenticator.TokenService.Verify(
            "not-a-valid-token",
            rsa.ExportRSAPublicKeyPem(),
            "Server A");

        Assert.False(result);
    }

    [Fact]
    public async Task Verify_ThrowsForNullToken()
    {
        using RSA rsa = TestableTokenService.CreateRsaKeyPair();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            PAM_Authenticator.TokenService.Verify(
                null!,
                rsa.ExportRSAPublicKeyPem(),
                "Server A"));
    }

    [Fact]
    public async Task Verify_ThrowsForEmptyToken()
    {
        using RSA rsa = TestableTokenService.CreateRsaKeyPair();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            PAM_Authenticator.TokenService.Verify(
                string.Empty,
                rsa.ExportRSAPublicKeyPem(),
                "Server A"));
    }

    [Fact]
    public async Task Verify_ThrowsForNullPublicKey()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            PAM_Authenticator.TokenService.Verify(
                "token",
                null!,
                "Server A"));
    }

    [Fact]
    public async Task Verify_ThrowsForNullAudience()
    {
        using RSA rsa = TestableTokenService.CreateRsaKeyPair();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            PAM_Authenticator.TokenService.Verify(
                "token",
                rsa.ExportRSAPublicKeyPem(),
                null!));
    }

    [Fact]
    public async Task Verify_ThrowsForMalformedPublicKey()
    {
        await Assert.ThrowsAsync<ArgumentException>(() =>
            PAM_Authenticator.TokenService.Verify(
                "token",
                "not-a-valid-public-key",
                "Server A"));
    }
}
