namespace Shared.Constants;

public static class UserActionConstants
{
    public static class Authentication
    {
        public const string Login = "user.login";
        public const string Logout = "user.logout";
        public const string RefreshToken = "user.refresh_token";
        public const string FailedLogin = "user.failed_login";
        public const string SessionExpired = "user.session_expired";
        public const string TokenGenerated = "user.token_generated";
    }

    public static class Registration
    {
        public const string Register = "user.register";
        public const string FailedRegistration = "user.failed_register";
        public const string EmailVerification = "user.email_verification";
        public const string AccountActivated = "user.account_activated";
    }

    public static class Account
    {
        public const string ChangePassword = "user.change_password";
        public const string ForgotPassword = "user.forgot_password";
        public const string UpdateProfile = "user.update_profile";
        public const string DeleteAccount = "user.delete_account";
        public const string LockAccount = "user.lock_account";
        public const string UnlockAccount = "user.unlock_account";
    }

    public static class Validation
    {
        public const string RoleValidation = "validation.role";
        public const string TierValidation = "validation.tier";
        public const string ModelValidation = "validation.model";
        public const string TokenValidation = "validation.token";
    }

    public static class System
    {
        public const string UnexpectedError = "system.unexpected_error";
        public const string DatabaseError =  "system.database_error";
        public const string ServiceUnavailable = "system.service_unavailable";
    }
}