using Shared.DTOs;

namespace AuthService.Services;

public interface IAuthService
{
    Task<AuthResponseDto?> LoginAsync(AuthRequestDto dto );
}