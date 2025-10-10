using Shared.DTOs;

namespace UserService.Services;

public interface IUserService
{
    Task<UserDto> AuthenticateUserAsync(UserDto dto, string activity);
}