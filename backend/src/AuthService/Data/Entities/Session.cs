namespace AuthService.Data.Entities;

public class Session
{
    public int Id { get; set; }
    public Guid SessionId { get; set; }
    public Guid UserId { get; set; }
    public string AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime IssueAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public string? DeviceInfo { get; set; }
    public string? IP { get; set; }
    public string Status { get; set; }
    public DateTime? RevokedAt { get; set; }
}