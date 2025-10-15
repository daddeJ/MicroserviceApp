using Shared.DTOs;

namespace AuthService.Services;

public interface IAuthService
{
    Task<(bool Success, string[] Errors)>  HandleAuthTokenEventAsync(Guid userId, string token, string op);
    Task HandleUserAuthenticationTokenAsync(Guid userId, string op);
}