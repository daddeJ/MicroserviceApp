using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Shared.Caching;
using Shared.DTOs;
using Shared.Helpers;
using UserService.Data;
using UserService.Helpers;
using UserService.Services;

namespace UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserService _userService;
    private readonly RedisCacheHelper  _redisCacheHelper;
    public UserController(
        UserManager<ApplicationUser> userManager,
        IUserService userService,
        RedisCacheHelper redisCacheHelper)
    {
        _userManager = userManager;
        _userService = userService;
        _redisCacheHelper = redisCacheHelper;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegistrationDto model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest("Model is invalid");
        }

        var user = new ApplicationUser
        {
            UserName = model.UserName,
            Email = model.Email
        };

        var result = await _userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            return BadRequest($"Failed to create user {model.UserName}: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        }

        if (string.IsNullOrEmpty(model.Role))
        {
            return BadRequest("Role is required");
        }
        await _userManager.AddToRoleAsync(user, model.Role);
        
        if (!DataSeeder.RoleTierMap.TryGetValue(model.Role, out var expectedTier) 
            || !string.Equals(expectedTier.ToString(), model.Tier.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { message = $"Tier '{model.Tier}' is not valid for role '{model.Role}'." });
        }
        
        await _userManager.AddClaimAsync(user, new Claim("Tier", model.Tier.ToString()));
        
        var userTemp = new UserDto
        {
            UserId = Guid.Parse(user.Id),
            UserName = user.UserName,
            Email = user.Email,
            Role = model.Role,
            Tier = model.Tier.ToString()
        };

        var userDetail = await _userService.RegistrationUserAsync(userTemp);
        var tokenKey = $"user:{userDetail.UserId}:token";
        var tokenValue = await _redisCacheHelper.GetStringAsync(tokenKey);

        return Ok(new
        {
            UserDto = userDetail,
            Token =  tokenValue,
        });
    }
}