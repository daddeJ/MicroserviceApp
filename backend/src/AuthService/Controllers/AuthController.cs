using AuthService.DTOs;
using AuthService.Services;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs;
using Shared.Models;

namespace AuthService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    
    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }
    
    [HttpPost("validate")]
    public async Task<IActionResult> ValidateToken([FromBody] TokenValidationRequestDto dto)
    {
        if (dto.UserId == Guid.Empty || string.IsNullOrWhiteSpace(dto.Token))
        {
            var response = ApiResponse<string>.Fail("Invalid request", "INVALID_REQUEST");
            return BadRequest(response);
        }

        var isValid = await _authService.HandleAuthTokenEventAsync(dto.UserId, dto.Token);

        if (isValid.Success)
        {
            var response = ApiResponse<object>.Ok(
                new { UserId = dto.UserId, Token = dto.Token },
                "Token is valid and activity logged."
            );
            return Ok(response);
        }

        var failedResponse = ApiResponse<object>.Fail("Token validation failed", "INVALID_TOKEN");
        return Unauthorized(failedResponse);
    }
}