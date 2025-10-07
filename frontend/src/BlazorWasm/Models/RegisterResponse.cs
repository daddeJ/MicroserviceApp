namespace BlazorWasm.Models;

public class RegisterResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = "";
    public string Token { get; set; } = "";
}