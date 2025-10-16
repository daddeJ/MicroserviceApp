using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

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
        var executionStrategy = _userDbContext.Database.CreateExecutionStrategy();
        
        return await executionStrategy.ExecuteAsync(async () =>
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

                var existingClaims = await _userManager.GetClaimsAsync(user);
                var tierClaim = existingClaims.FirstOrDefault(c => c.Type == "Tier");

                if (tierClaim != null)
                {
                    var removeResult = await _userManager.RemoveClaimAsync(user, tierClaim);
                    if (!removeResult.Succeeded)
                    {
                        await transaction.RollbackAsync();
                        return (false, removeResult.Errors.Select(e => e.Description).ToArray());
                    }
                }

                var addClaimResult = await _userManager.AddClaimAsync(user, new Claim("Tier", tier.ToString()));
                if (!addClaimResult.Succeeded)
                {
                    await transaction.RollbackAsync();
                    return (false, addClaimResult.Errors.Select(e => e.Description).ToArray());
                }

                await transaction.CommitAsync();
                return (true, Array.Empty<string>());
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return (false, new[] { ex.Message });
            }
        });
    }
}