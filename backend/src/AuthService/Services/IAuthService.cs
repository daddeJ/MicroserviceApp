using Shared.DTOs;

namespace AuthService.Services;

public interface IAuthService
{
    Task HandleAuthTokenEventAsync(Guid userId, string token);
    Task HandleUserRegisteredAsync(Guid userId);
}