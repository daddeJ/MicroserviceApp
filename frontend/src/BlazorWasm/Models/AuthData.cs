namespace BlazorWasm.Models;

public class AuthData
{
    public UserInfo User { get; set; } = default!;
    public string Token { get; set; } = string.Empty;
}