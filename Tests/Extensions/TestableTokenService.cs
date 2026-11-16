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

        RSA rsa = RSA.Create();

        rsa.ImportFromPem(privateKey);

        RsaSecurityKey rsaSecurityKey = new(rsa);
        SigningCredentials signingCredentials = new(rsaSecurityKey, SecurityAlgorithms.RsaSha512);

        JwtSecurityToken token = new(
            issuer: issuer,
            audience: audience,
            expires: DateTime.UtcNow.AddDays(1),
            signingCredentials: signingCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public static RSA CreateRsaKeyPair()
    {
        return RSA.Create(4096);
    }
}
