using System.Security.Claims;
using Microsoft.AspNetCore.Identity;

namespace UserService.Data.Transactions;

public class UserRegistrationTransaction : IUserRegistrationTransaction
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly UserDbContext _userDbContext;

    public UserRegistrationTransaction(UserDbContext dbContext, UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
        _userDbContext = dbContext;
    }
    public async Task<(bool success, string[] error)> RegisterUserTransactionAsync(
        ApplicationUser user,
        string password,
        string role,
        int tier)
    {
        await using var transaction = await _userDbContext.Database.BeginTransactionAsync();

        try
        {
            var createResult = await _userManager.CreateAsync(user, password);
            if (!createResult.Succeeded)
            {
                await transaction.RollbackAsync();
                return (false, createResult.Errors.Select(e => e.Description).ToArray());
            }

            var roleResult = await _userManager.AddToRoleAsync(user, role);
            if (!roleResult.Succeeded)
            {
                await transaction.RollbackAsync();
                return (false, roleResult.Errors.Select(e => e.Description).ToArray());
            }

            var existingClaim = await _userManager.GetClaimsAsync(user);
            if (existingClaim.Any(c => c.Type == "Tier"))
            {
                var claimResult = await _userManager.AddClaimAsync(user, new Claim("Tier", tier.ToString()));
                if (!claimResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return (false, claimResult.Errors.Select(e => e.Description).ToArray());
                }
            }

            await transaction.CommitAsync();
            return (true, Array.Empty<string>());
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return (false, new[] { ex.Message });
        }
    }
}