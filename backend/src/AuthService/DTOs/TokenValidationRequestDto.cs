namespace AuthService.DTOs;

public class TokenValidationRequestDto
{
    public Guid UserId { get; set; }
    public string Token { get; set; } = string.Empty;
    public string Operation  { get; set; } = string.Empty;
}