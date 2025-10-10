using AuthService.DTOs;

namespace AuthService.Services;

public interface ISessionService
{
    Task<IEnumerable<SessionDto>> GetAllActiveSessionsAsync();
    Task<IEnumerable<SessionDto>> GetUserSessionsAsync(Guid userId);
    Task<object> GetSessionStatisticsAsync();
    Task<int> RevokeSessionAsync(Guid userId, string? token = null);
    Task<int> RevokeAllUserSessionsAsync(Guid userId);
}