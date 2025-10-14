namespace BlazorWasm.Models;

public class LoginRequest
{
    public string? Email { get; set; } = null;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}