using System.ComponentModel.DataAnnotations;
using Shared.DTOs;
using UserService.Builders;
using UserService.DTOs;

namespace UserService.Helpers;

public static class QueryValidationHelper
{
    public static bool TryValidateIntList(
        string? input,
        List<int> allowedValues,
        out List<int> result,
        out string? errorMessage)
    {
        result = new List<int>();
        errorMessage = null;

        if (string.IsNullOrEmpty(input))
            return true;

        try
        {
            result = input.Split(',')
                .Select(x => int.Parse(x.Trim()))
                .ToList();
        }
        catch (Exception e)
        {
            errorMessage = "Invalid integer format.";
            return false;
            
        }

        if (result.Any(x => !allowedValues.Contains(x)))
        {
            errorMessage = $"Allowed values: {string.Join(", ", allowedValues)}";
            return false;
        }
        
        return true;
    }

    public static bool TryValidateStringList(
        string? input,
        List<string> allowedValues,
        out List<string> result,
        out string? errorMessage)
    {
        result = new List<string>();
        errorMessage = null;
        
        if (string.IsNullOrEmpty(input))
            return true; 
        
        result = input.Split(',')
            .Select(x => x.Trim())
            .ToList();

        if (result.Any(x => !allowedValues.Contains(x)))
        {
            errorMessage = $"Allowed roles: {string.Join(", ", allowedValues)}";
            return false;
        }
        
        return true;
    }
    
    public static ValidationBuilder<RegistrationDto> BuildRegistrationValidation(RegistrationDto model)
    {
        return new ValidationBuilder<RegistrationDto>(model)
            .Rule(m => !string.IsNullOrEmpty(m.Email) && new EmailAddressAttribute().IsValid(m.Email),
                "Email", "Invalid email address")
            .Rule(m => !string.IsNullOrEmpty(m.UserName) && m.UserName.Length >= 6,
                "UserName", "UserName must have at least 6 characters")
            .Rule(m => !string.IsNullOrEmpty(m.Password) && m.Password.Length >= 6,
                "Password", "Password must have at least 6 characters")
            .Rule(m => m.Password == m.ConfirmPassword,
                "ConfirmPassword", "Passwords do not match")
            .Rule(m => QueryValidationHelper.TryValidateStringList(m.Role, DataSeeder.RoleTierMap.Keys.ToList(), out _, out var error),
                "Role", "Invalid role")
            .Rule(m => QueryValidationHelper.TryValidateIntList(m.Tier.ToString(), Enumerable.Range(0, 6).ToList(), out _, out var error),
                "Tier", "Invalid tier");
    }
    
    public static ValidationBuilder<LoginDto> BuildLoginValidation(LoginDto model)
    {
        return new ValidationBuilder<LoginDto>(model)
            .Rule(m => !string.IsNullOrEmpty(m.Username) && m.Username.Length >= 6,
                "Username", "Username must have at least 6 characters")
            .Rule(m => !string.IsNullOrEmpty(m.Password) && m.Password.Length >= 6,
                "Password", "Password must have at least 6 characters");
    }
}