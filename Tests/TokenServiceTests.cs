using System.Security.Cryptography;
using Tests.Extensions;

namespace Tests;

public class TokenServiceTestsTest
{
    [Fact]
    public async Task Verify_ReturnsTrue_ForValidToken()
    {
        using ECDsa ecdsa = TestableTokenService.CreateEcdsaKeyPair();

        string token = TestableTokenService.CreateAndSignToken(
            ecdsa.ExportECPrivateKeyPem(),
            "Fileodon Master Server",
            "Server A");

        bool result = await PAM_Authenticator.TokenService.Verify(
            token,
            ecdsa.ExportSubjectPublicKeyInfoPem(),
            "Server A");

        Assert.True(result);
    }

    [Fact]
    public async Task Verify_ReturnsFalse_ForInvalidIssuer()
    {
        using ECDsa ecdsa = TestableTokenService.CreateEcdsaKeyPair();

        string token = TestableTokenService.CreateAndSignToken(
            ecdsa.ExportECPrivateKeyPem(),
            "Invalid Issuer",
            "Server A");

        bool result = await PAM_Authenticator.TokenService.Verify(
            token,
            ecdsa.ExportSubjectPublicKeyInfoPem(),
            "Server A");

        Assert.False(result);
    }

    [Fact]
    public async Task Verify_ReturnsFalse_ForInvalidAudience()
    {
        using ECDsa ecdsa = TestableTokenService.CreateEcdsaKeyPair();

        string token = TestableTokenService.CreateAndSignToken(
            ecdsa.ExportECPrivateKeyPem(),
            "Fileodon Master Server",
            "Server A");

        bool result = await PAM_Authenticator.TokenService.Verify(
            token,
            ecdsa.ExportSubjectPublicKeyInfoPem(),
            "Server B");

        Assert.False(result);
    }

    [Fact]
    public async Task Verify_ReturnsFalse_WhenSignatureWasCreatedWithDifferentKey()
    {
        using ECDsa signingEcdsa = TestableTokenService.CreateEcdsaKeyPair();
        using ECDsa validationEcdsa = TestableTokenService.CreateEcdsaKeyPair();

        string token = TestableTokenService.CreateAndSignToken(
            signingEcdsa.ExportECPrivateKeyPem(),
            "Fileodon Master Server",
            "Server A");

        bool result = await PAM_Authenticator.TokenService.Verify(
            token,
            validationEcdsa.ExportSubjectPublicKeyInfoPem(),
            "Server A");

        Assert.False(result);
    }

    [Fact]
    public async Task Verify_ReturnsFalse_ForMalformedToken()
    {
        using ECDsa ecdsa = TestableTokenService.CreateEcdsaKeyPair();

        bool result = await PAM_Authenticator.TokenService.Verify(
            "not-a-valid-token",
            ecdsa.ExportSubjectPublicKeyInfoPem(),
            "Server A");

        Assert.False(result);
    }

    [Fact]
    public async Task Verify_ThrowsForNullToken()
    {
        using ECDsa ecdsa = TestableTokenService.CreateEcdsaKeyPair();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            PAM_Authenticator.TokenService.Verify(
                null!,
                ecdsa.ExportSubjectPublicKeyInfoPem(),
                "Server A"));
    }

    [Fact]
    public async Task Verify_ThrowsForEmptyToken()
    {
        using ECDsa ecdsa = TestableTokenService.CreateEcdsaKeyPair();

        await Assert.ThrowsAsync<ArgumentException>(() =>
            PAM_Authenticator.TokenService.Verify(
                string.Empty,
                ecdsa.ExportSubjectPublicKeyInfoPem(),
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
        using ECDsa ecdsa = TestableTokenService.CreateEcdsaKeyPair();

        await Assert.ThrowsAsync<ArgumentNullException>(() =>
            PAM_Authenticator.TokenService.Verify(
                "token",
                ecdsa.ExportSubjectPublicKeyInfoPem(),
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
