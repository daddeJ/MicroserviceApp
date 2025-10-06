using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Shared.Helpers;

public class JwtHelper
{
    private readonly RSA _privateKey;
    private readonly RSA _publicKey;

    public JwtHelper(string privateKeyPem, string publicKeyPem)
    {
        if (string.IsNullOrEmpty(privateKeyPem))
            throw new ArgumentNullException(nameof(privateKeyPem), "Private key cannot be null or empty.");
        if (string.IsNullOrEmpty(publicKeyPem))
            throw new ArgumentNullException(nameof(publicKeyPem), "Public key cannot be null or empty.");
        
        _privateKey = RSA.Create();
        _privateKey.ImportFromPem(privateKeyPem.ToCharArray());
        
        _publicKey = RSA.Create();
        _publicKey.ImportFromPem(publicKeyPem.ToCharArray());
    }
    public string GenerateToken(Guid userId, string username, int expireMinutes = 60)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Name, username)
            }),
            Expires = DateTime.UtcNow.AddMinutes(expireMinutes),
            SigningCredentials = new SigningCredentials(
                new RsaSecurityKey(_privateKey),
                SecurityAlgorithms.RsaSha256
                )
        };
        
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }

    public ClaimsPrincipal? ValidateToken(string token)
    {
        var tokenHandler = new JwtSecurityTokenHandler();
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = true,
            IssuerSigningKey = new RsaSecurityKey(_publicKey),
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            var principal = tokenHandler.ValidateToken(token, validationParameters, out _);
            return principal;
        }
        catch
        {
            return null;
        }
    }
}