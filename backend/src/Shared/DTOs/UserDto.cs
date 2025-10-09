namespace Shared.DTOs;

public sealed class UserDto
{
    public Guid UserId { get; set; }
    public string UserName { get; set; }
    public string Email { get; set; }
    public string Role { get; set; }
    public string Tier { get; set; }
}