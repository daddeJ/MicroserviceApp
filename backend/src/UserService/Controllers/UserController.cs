using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Shared.Caching;
using Shared.Constants;
using Shared.DTOs;
using UserService.Builders;
using UserService.Data;
using UserService.Data.Transactions;
using UserService.DTOs;
using UserService.Helpers;
using UserService.Services;

namespace UserService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class UserController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IUserService _userService;
    private readonly RedisCacheHelper _redisCacheHelper;
    private readonly IUserRegistrationTransaction _userRegistrationTransaction;
    private readonly IPublisherService _publisherService;

    public UserController(
        UserManager<ApplicationUser> userManager,
        IUserService userService,
        RedisCacheHelper redisCacheHelper,
        IUserRegistrationTransaction userRegistrationTransaction,
        IPublisherService publisherService)
    {
        _userManager = userManager;
        _userService = userService;
        _redisCacheHelper = redisCacheHelper;
        _userRegistrationTransaction = userRegistrationTransaction;
        _publisherService = publisherService;
    }

    [HttpPost("register")]
    [AllowAnonymous]
    public async Task<IActionResult> Register([FromBody] RegistrationDto model)
    {
        var validationError = ApiResponseFactory.ValidateModel(model);
        if (validationError != null)
        {
            await _publisherService.PublishLogAsync(Guid.Empty, UserActionConstants.Validation.ModelValidation);
            return validationError;
        }

        var user = new ApplicationUser { UserName = model.UserName, Email = model.Email };

        if (string.IsNullOrEmpty(model.Role))
        {
            await _publisherService.PublishLogAsync(Guid.Empty, UserActionConstants.Validation.RoleValidation);
            return ApiResponseFactory.ValidationError("Role", "Role is required");
        }

        if (!DataSeeder.RoleTierMap.TryGetValue(model.Role, out var expectedTier))
        {
            await _publisherService.PublishLogAsync(Guid.Empty, UserActionConstants.Validation.RoleValidation);
            return ApiResponseFactory.InvalidAllowedValues("Role", model.Role, DataSeeder.RoleTierMap.Keys);
        }

        if (!string.Equals(expectedTier.ToString(), model.Tier.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            await _publisherService.PublishLogAsync(Guid.Empty, UserActionConstants.Validation.TierValidation);
            return ApiResponseFactory.InvalidAllowedValues("Tier", model.Tier, new[] { expectedTier });
        }

        var (success, error) = await _userRegistrationTransaction.RegisterUserTransactionAsync(user, model.Password, model.Role, model.Tier);

        if (!success)
        {
            await _publisherService.PublishLogAsync(Guid.Parse(user.Id), UserActionConstants.Registration.FailedRegistration);
            return ApiResponseFactory.ValidationCustom(
                "User creation failed", 
                details: new Dictionary<string, string[]> { { "IdentityErrors", error } });
        }

        await _publisherService.PublishLogAsync(Guid.Parse(user.Id), UserActionConstants.Registration.Register);

        var userTemp = new UserDto
        {
            UserId = Guid.Parse(user.Id),
            UserName = user.UserName,
            Email = user.Email,
            Role = model.Role,
            Tier = model.Tier.ToString()
        };

        var userDetail = await _userService.AuthenticateUserAsync(userTemp, QueueNames.UserActionRegister);
        var tokenValue = await _redisCacheHelper.WaitForValueAsync($"user:{userDetail.UserId}:{UserActionConstants.Validation.RegisteredTokenValidation}");

        return ApiResponseFactory.Ok(new { User = userDetail, Token = tokenValue }, "User registered successfully");
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginDto model)
    {
        // Validate model using ApiResponseFactory
        var validationError = ApiResponseFactory.ValidateModel(model);
        if (validationError != null)
        {
            await _publisherService.PublishLogAsync(Guid.Empty, UserActionConstants.Validation.ModelValidation, UserActionConstants.Validation.LoginTokenValidation);
            return validationError;
        }

        var user = await _userManager.FindByNameAsync(model.Username);
        if (user == null)
        {
            await _publisherService.PublishLogAsync(Guid.Empty, UserActionConstants.Authentication.FailedLogin, model.Username);
            return ApiResponseFactory.Unauthorized<object>("Invalid username or password.");
        }

        var passwordValid = await _userManager.CheckPasswordAsync(user, model.Password);
        if (!passwordValid)
        {
            await _publisherService.PublishLogAsync(Guid.Parse(user.Id), UserActionConstants.Authentication.FailedLogin, UserActionConstants.Validation.LoginTokenValidation);
            return ApiResponseFactory.Unauthorized<object>("Invalid username or password.");
        }

        var roles = await _userManager.GetRolesAsync(user);
        var tiers = await _userManager.GetClaimsAsync(user);
        var tierClaim = tiers.FirstOrDefault(c => c.Type == "Tier")?.Value;

        var userTemp = new UserDto
        {
            UserId = Guid.Parse(user.Id),
            UserName = user.UserName ?? string.Empty,
            Email = user.Email ?? string.Empty,
            Role = roles.FirstOrDefault() ?? string.Empty,
            Tier = tierClaim ?? string.Empty,
        };

        var userDetail = await _userService.AuthenticateUserAsync(userTemp, QueueNames.UserActionLogin);

        var tokenKey = $"user:{userDetail.UserId}:{UserActionConstants.Validation.LoginTokenValidation}";
        var tokenValue = await _redisCacheHelper.WaitForValueAsync(tokenKey);

        await _publisherService.PublishLogAsync(userDetail.UserId, UserActionConstants.Authentication.Login, UserActionConstants.Validation.LoginTokenValidation);

        return ApiResponseFactory.Ok(new
        {
            User = userDetail,
            Token = tokenValue
        }, "Login successful");
    }
}