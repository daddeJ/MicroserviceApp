using Microsoft.AspNetCore.Mvc;
using Shared.DTOs;
using Shared.Helpers;
using UserService.Services;

namespace UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly IUserService _userService;
    private readonly RedisConnectionHelper _redisHelper;
    private readonly IHostEnvironment _env;
    public UserController(
        IUserService userService,
        RedisConnectionHelper redisHelper,
        IHostEnvironment env)
    {
        _userService = userService;
        _redisHelper = redisHelper;
        _env = env ?? throw new ArgumentNullException(nameof(env));
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] UserRegistrationDto userRegistration)
    {
        var user = await _userService.RegistrationUserAsync(userRegistration);
        
        await Task.Delay(500); 

        var db = _redisHelper.GetDatabase();
        var tokenKey = $"user:{user.UserId}:token";
        var tokenValue = await db.StringGetAsync(tokenKey);

        return Ok(new
        {
            UserId = user.UserId,
            Username = user.Username,
            Token = tokenValue.ToString()
        });
    }
}