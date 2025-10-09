using System.Security.Cryptography;

namespace Shared.Security;

public static class RsaKeyHelper
{
    public static RSA LoadPrivateKey(string pem)
    {
        if (string.IsNullOrEmpty(pem))
            throw new ArgumentNullException(nameof(pem), "Private key cannot be null or empty.");
        var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        return rsa;
    }
    
    public static RSA LoadPublicKey(string pem)
    {
        if (string.IsNullOrEmpty(pem))
            throw new ArgumentNullException(nameof(pem), "Public key cannot be null or empty.");
        var rsa = RSA.Create();
        rsa.ImportFromPem(pem);
        return rsa;
    }
}