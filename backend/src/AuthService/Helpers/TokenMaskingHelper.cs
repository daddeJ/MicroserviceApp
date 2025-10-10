namespace AuthService.Helpers;

public static class TokenMaskingHelper
{
    public static string MaskToken(string? token)
    {
        if (string.IsNullOrEmpty(token)) return string.Empty;
        
        return token.Length <= 4
            ? "****"
            : $"****{token[^4..]}";
    }
}