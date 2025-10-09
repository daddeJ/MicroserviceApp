namespace Shared.DTOs;

public sealed class LoginDto
{
    public string? Email { get; set; } = null;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}