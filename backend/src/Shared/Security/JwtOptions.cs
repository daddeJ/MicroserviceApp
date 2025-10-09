namespace Shared.Security;

public class JwtOptions
{
    public string Issuer { get; set; }
    public string Audience { get; set; }
    public string PrivateKey { get; set; }
    public string PublicKey { get; set; }
    public int ExpirationMinutes { get; set; }
}