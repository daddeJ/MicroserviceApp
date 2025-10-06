using Shared.DTOs;

namespace UserService.Services;

public interface IUserService
{
    Task<UserRegistrationDto> RegistrationUserAsync(UserRegistrationDto dto);
    Task HandleAuthTokenEventAsync(Guid userId, string token);
}