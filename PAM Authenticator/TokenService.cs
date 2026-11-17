using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Serilog;

namespace PAM_Authenticator;

public class TokenService
{
    public static async Task<bool> Verify(string token, string publicKey, string audience)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        ArgumentException.ThrowIfNullOrEmpty(publicKey);
        ArgumentException.ThrowIfNullOrEmpty(audience);

        using ECDsa ecdsa = ECDsa.Create();

        ecdsa.ImportFromPem(publicKey);

        TokenValidationParameters validationParameters = new()
        {
            ValidateIssuer = true,
            ValidIssuer = "Fileodon Master Server",
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new ECDsaSecurityKey(ecdsa),
        };

        JsonWebTokenHandler handler = new();

        try
        {
            TokenValidationResult principal = await handler.ValidateTokenAsync(token, validationParameters);
            if (principal.IsValid)
            {
                return true;
            }
            else
            {
                Log.Warning(principal.Exception, "Token validation failed.");
                Console.WriteLine($"Token validation failed: {principal.Exception?.Message}");
                return false;
            }
        }
        catch (SecurityTokenException ex)
        {
            Log.Warning(ex, "Token validation failed.");
            Console.WriteLine($"Token validation failed: {ex.Message}");
            return false;
        }
    }
}
