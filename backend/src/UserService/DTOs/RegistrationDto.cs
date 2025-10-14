using System.ComponentModel.DataAnnotations;

namespace UserService.DTOs;

public class RegistrationDto
{
    public string Email { get; set; }
    
    public string UserName { get; set; }
    
    public string Password { get; set; }
    
    public string ConfirmPassword { get; set; }
    
    public string Role { get; set; }
    
    public int Tier { get; set; } = 5;
}