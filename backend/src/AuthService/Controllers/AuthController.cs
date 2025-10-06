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

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] AuthRequestDto request)
    {
        var result = await _authService.LoginAsync(request);
        if (result == null) 
            return Unauthorized(new { message = "Invalid credentials"});
        return Ok(result);
    }
}