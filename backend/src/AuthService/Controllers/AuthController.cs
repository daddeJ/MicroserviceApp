using AuthService.DTOs;
using AuthService.Services;
using Microsoft.AspNetCore.Identity.Data;
using Microsoft.AspNetCore.Mvc;
using Shared.DTOs;

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
            return BadRequest("Invalid request");

        // Call AuthService to validate token
        await _authService.HandleAuthTokenEventAsync(dto.UserId, dto.Token);

        return Ok(new { Message = "Token validation attempted. Activity logged if valid." });
    }
}