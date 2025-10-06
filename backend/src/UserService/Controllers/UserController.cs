using Microsoft.AspNetCore.Mvc;
using Shared.DTOs;
using UserService.Services;

namespace UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;

    public UserController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] UserRegistrationDto userRegistration)
    {
        var user = await _userService.RegistrationUserAsync(userRegistration);
        return Ok(user);
    }
}