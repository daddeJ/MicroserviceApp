using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace Shared.Security;

public class TokenValidator
{
    private readonly JwtOptions _options;
    
    public TokenValidator(JwtOptions options) => _options = options;

    public ClaimsPrincipal? ValidateToken(string token)
    {
        using var rsa = RsaKeyHelper.LoadPublicKey(_options.PublicKey);

        var parameters = new TokenValidationParameters
        {
            ValidIssuer = _options.Issuer,
            ValidAudience = _options.Audience,
            IssuerSigningKey = new RsaSecurityKey(rsa),
            ValidateAudience = true,
            ValidateIssuerSigningKey = true
        };

        var handler = new JwtSecurityTokenHandler();
        try
        {
            return handler.ValidateToken(token, parameters, out _);
        }
        catch
        {
            return null;
        }
    }
}