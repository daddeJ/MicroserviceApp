using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Shared.Caching;
using Shared.Constants;
using Shared.DTOs;
using Shared.Helpers;
using Shared.Models;
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
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegistrationDto model)
    {
        if (!ModelState.IsValid)
            return ApiResponseFactory.ValidationFromModelState(ModelState);

        var user = new ApplicationUser { UserName = model.UserName, Email = model.Email };
        var result = await _userManager.CreateAsync(user, model.Password);

        if (!result.Succeeded)
        {
            var identityErrors = result.Errors.Select(e => e.Description).ToArray();
            var errors = new Dictionary<string, string[]> { { "IdentityErrors", identityErrors } };
            return ApiResponseFactory.ValidationCustom("User creation failed", "USR_001", errors);
        }

        if (string.IsNullOrEmpty(model.Role))
            return ApiResponseFactory.ValidationError("Role", "Role is required", "ROLE_001");

        if (!DataSeeder.RoleTierMap.TryGetValue(model.Role, out var expectedTier))
            return ApiResponseFactory.InvalidAllowedValues("Role", model.Role, DataSeeder.RoleTierMap.Keys);

        if (!string.Equals(expectedTier.ToString(), model.Tier.ToString(), StringComparison.OrdinalIgnoreCase))
            return ApiResponseFactory.InvalidAllowedValues("Tier", model.Tier, new[] { expectedTier });

        var userTemp = new UserDto
        {
            UserId = Guid.Parse(user.Id),
            UserName = user.UserName,
            Email = user.Email,
            Role = model.Role,
            Tier = model.Tier.ToString()
        };

        var userDetail = await _userService.AuthenticateUserAsync(userTemp, QueueNames.UserActionRegister);
        var tokenValue = await _redisCacheHelper.WaitForValueAsync($"user:{userDetail.UserId}:token");

        return ApiResponseFactory.Ok(new { User = userDetail, Token = tokenValue }, "User registered successfully");
    }
    
    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDto model)
    {
        if (!ModelState.IsValid)
            return ApiResponseFactory.ValidationFromModelState(ModelState);

        var user = await _userManager.FindByNameAsync(model.Username);
        if (user == null)
            return ApiResponseFactory.Unauthorized<object>("Invalid username or password.");

        var passwordValid = await _userManager.CheckPasswordAsync(user, model.Password);
        if (!passwordValid)
            return ApiResponseFactory.Unauthorized<object>("Invalid username or password.");

        var roles = await _userManager.GetRolesAsync(user);
        var tiers = await _userManager.GetClaimsAsync(user);
        var tierClaim = tiers.FirstOrDefault(c => c.Type == "Tier")?.Value;

        var userTemp = new UserDto
        {
            UserId = Guid.Parse(user.Id),
            UserName = user.UserName ?? String.Empty,
            Email = user.Email ?? String.Empty,
            Role = roles.FirstOrDefault() ?? String.Empty,
            Tier = tierClaim ??  String.Empty,
        };

        var userDetail = await _userService.AuthenticateUserAsync(userTemp, QueueNames.UserActionLogin);
        var tokenKey = $"user:{userDetail.UserId}:token";
        var tokenValue = await _redisCacheHelper.WaitForValueAsync(tokenKey);

        return ApiResponseFactory.Ok(new
        {
            User = userDetail,
            Token = tokenValue
        }, "Login successful");
    }
}