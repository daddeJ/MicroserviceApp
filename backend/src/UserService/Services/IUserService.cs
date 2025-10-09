using Shared.DTOs;

namespace UserService.Services;

public interface IUserService
{
    Task<UserDto> RegistrationUserAsync(UserDto dto);
}