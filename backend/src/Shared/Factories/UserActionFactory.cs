using Shared.Constants;

namespace Shared.Factories;

public class UserActionFactory : IUserActionFactory
{
    public UserActionMetadata GetMetadata(string action)
    {
        return action switch
            {
                UserActionConstants.Authentication.Login => new UserActionMetadata
                {
                    Action = action,
                    Category = "Authentication",
                    Description = "User successfully logged in",
                    DefaultLogLevel = "Information"
                },
                UserActionConstants.Authentication.Logout => new UserActionMetadata
                {
                    Action = action,
                    Category = "Authentication",
                    Description = "User logged out successfully",
                    DefaultLogLevel = "Information"
                },
                UserActionConstants.Authentication.FailedLogin => new UserActionMetadata
                {
                    Action = action,
                    Category = "Authentication",
                    Description = "User failed login attempt",
                    DefaultLogLevel = "Warning"
                },
                UserActionConstants.Authentication.SessionExpired => new UserActionMetadata
                {
                    Action = action,
                    Category = "Authentication",
                    Description = "Session expired",
                    DefaultLogLevel = "Warning"
                },

                UserActionConstants.Registration.Register => new UserActionMetadata
                {
                    Action = action,
                    Category = "Registration",
                    Description = "User registered successfully",
                    DefaultLogLevel = "Information"
                },
                UserActionConstants.Registration.FailedRegistration => new UserActionMetadata
                {
                    Action = action,
                    Category = "Registration",
                    Description = "User failed registration",
                    DefaultLogLevel = "Warning"
                },
                UserActionConstants.Registration.EmailVerification => new UserActionMetadata
                {
                    Action = action,
                    Category = "Registration",
                    Description = "Email verification process started",
                    DefaultLogLevel = "Information"
                },
                UserActionConstants.Registration.AccountActivated => new UserActionMetadata
                {
                    Action = action,
                    Category = "Registration",
                    Description = "User account activated",
                    DefaultLogLevel = "Information"
                },

                UserActionConstants.Account.ChangePassword => new UserActionMetadata
                {
                    Action = action,
                    Category = "Account",
                    Description = "User changed password",
                    DefaultLogLevel = "Information"
                },
                UserActionConstants.Account.ForgotPassword => new UserActionMetadata
                {
                    Action = action,
                    Category = "Account",
                    Description = "User requested password reset",
                    DefaultLogLevel = "Information"
                },
                UserActionConstants.Account.UpdateProfile => new UserActionMetadata
                {
                    Action = action,
                    Category = "Account",
                    Description = "User updated profile information",
                    DefaultLogLevel = "Information"
                },
                UserActionConstants.Account.DeleteAccount => new UserActionMetadata
                {
                    Action = action,
                    Category = "Account",
                    Description = "User deleted their account",
                    DefaultLogLevel = "Warning"
                },

                UserActionConstants.Validation.ModelValidation => new UserActionMetadata
                {
                    Action = action,
                    Category = "Validation",
                    Description = "Model validation failed",
                    DefaultLogLevel = "Warning"
                },
                UserActionConstants.Validation.RoleValidation => new UserActionMetadata
                {
                    Action = action,
                    Category = "Validation",
                    Description = "Role validation failed",
                    DefaultLogLevel = "Warning"
                },
                UserActionConstants.Validation.TierValidation => new UserActionMetadata
                {
                    Action = action,
                    Category = "Validation",
                    Description = "Tier validation failed",
                    DefaultLogLevel = "Warning"
                },
                UserActionConstants.Validation.TokenValidation => new UserActionMetadata
                {
                    Action = action,
                    Category = "Validation",
                    Description = "Token validation failed",
                    DefaultLogLevel = "Error"
                },

                UserActionConstants.System.UnexpectedError => new UserActionMetadata
                {
                    Action = action,
                    Category = "System",
                    Description = "Unexpected system error occurred",
                    DefaultLogLevel = "Error"
                },
                UserActionConstants.System.DatabaseError => new UserActionMetadata
                {
                    Action = action,
                    Category = "System",
                    Description = "Database operation failed",
                    DefaultLogLevel = "Error"
                },
                UserActionConstants.System.ServiceUnavailable => new UserActionMetadata
                {
                    Action = action,
                    Category = "System",
                    Description = "Dependent service unavailable",
                    DefaultLogLevel = "Error"
                },

                _ => new UserActionMetadata
                {
                    Action = action,
                    Category = "Unknown",
                    Description = "Unrecognized user action",
                    DefaultLogLevel = "Debug"
                }
            };
    }
}