using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.IdentityModel.Tokens;

namespace Shared.Security;

public class JwtTokenGenerator
{
    private readonly JwtOptions _options;
    
    public JwtTokenGenerator(JwtOptions options) => _options = options;
    
    public string GenerateToken(IEnumerable<Claim> claims)
    {
        var rsa = RsaKeyHelper.LoadPrivateKey(_options.PrivateKey);
        
        var tokenHandler = new JwtSecurityTokenHandler();
        var tokenDescriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Expires = DateTime.UtcNow.AddMinutes(_options.ExpirationMinutes),
            SigningCredentials = new SigningCredentials(
                new RsaSecurityKey(rsa),
                SecurityAlgorithms.RsaSha256
            )
        };
        
        var token = tokenHandler.CreateToken(tokenDescriptor);
        return tokenHandler.WriteToken(token);
    }
}