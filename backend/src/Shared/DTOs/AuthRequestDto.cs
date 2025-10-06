namespace Shared.DTOs;

public sealed class AuthRequestDto
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}