using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using PAM_Authenticator;

namespace Tests.Extensions;

public class TestableTokenService : TokenService
{
    public static string CreateAndSignToken(string privateKey, string issuer, string audience)
    {
        ArgumentException.ThrowIfNullOrEmpty(privateKey);
        ArgumentException.ThrowIfNullOrEmpty(issuer);
        ArgumentException.ThrowIfNullOrEmpty(audience);

        using ECDsa ecdsa = ECDsa.Create();

        ecdsa.ImportFromPem(privateKey);

        ECDsaSecurityKey ecdsaSecurityKey = new(ecdsa);
        SigningCredentials signingCredentials = new(ecdsaSecurityKey, SecurityAlgorithms.EcdsaSha256);

        JwtSecurityToken token = new(
            issuer: issuer,
            audience: audience,
            expires: DateTime.UtcNow.AddDays(1),
            signingCredentials: signingCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static ECDsa CreateEcdsaKeyPair()
    {
        return ECDsa.Create(ECCurve.NamedCurves.nistP256);
    }
}
