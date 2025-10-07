namespace BlazorWasm.Models;

public class AuthResponse
{
    public string Token { get; set; } = string.Empty;
    public DateTime Expires { get; set; }
    public string? RefreshToken { get; set; }
}