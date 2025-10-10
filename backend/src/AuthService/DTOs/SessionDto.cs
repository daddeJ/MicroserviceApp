namespace AuthService.DTOs;

public class SessionDto
{
    public Guid UserId { get; set; }
    public string AccessToken { get; set; } = string.Empty;
    public string? RefreshToken { get; set; }
    public DateTime IssuedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
    public string? DeviceInfo { get; set; }
    public string? IP { get; set; }
    public string Status { get; set; } = string.Empty;
}