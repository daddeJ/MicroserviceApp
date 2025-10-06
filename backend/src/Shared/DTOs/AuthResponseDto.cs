namespace Shared.DTOs;

public sealed class AuthResponseDto
{
    public string Token { get; set; } = string.Empty;
    public DateTime Expires { get; set; }
    public string? RefreshToken { get; set; }
}