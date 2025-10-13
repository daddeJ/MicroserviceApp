namespace UserService.Data.Transactions;

public interface IUserRegistrationTransaction
{
    Task<(bool success, string[] error)> RegisterUserTransactionAsync(
        ApplicationUser user,
        string password,
        string role,
        int tier);
}