using System.Security.Claims;
using Shared.Caching;
using Shared.Constants;
using Shared.DTOs;
using Shared.Events;
using Shared.Factories;
using Shared.Interfaces;
using Shared.Security;
using ClaimTypes = Shared.Constants.ClaimTypes;

namespace AuthService.Services
{
    public class AuthServiceImp : IAuthService
    {
        private readonly JwtTokenGenerator _generator;
        private readonly RedisCacheHelper _redisCache;
        private readonly IMessagePublisher _messagePublisher;
        private readonly IUserActionFactory _userActionFactory;

        public AuthServiceImp(
            JwtTokenGenerator generator,
            RedisCacheHelper redisCache,
            IMessagePublisher messagePublisher,
            IUserActionFactory userActionFactory)
        {
            _generator = generator;
            _redisCache = redisCache;
            _messagePublisher = messagePublisher;
            _userActionFactory = userActionFactory;
        }
        
        public async Task<(bool Success, string[] Errors)> HandleAuthTokenEventAsync(Guid userId, string token, string op)
        {
            var errors = new List<string>();

            if (userId == Guid.Empty)
                errors.Add("UserId is invalid.");

            if (string.IsNullOrWhiteSpace(token))
                errors.Add("Token is required.");

            if (errors.Count > 0)
                return (false, errors.ToArray());

            string _op = op switch
            {
                QueueNames.UserActionLogin => UserActionConstants.Validation.LoginTokenValidation,
                QueueNames.UserActionRegister => UserActionConstants.Validation.RegisteredTokenValidation,
                _ => "Error"
            };

            if (_op.Equals("Error", StringComparison.OrdinalIgnoreCase))
            {
                errors.Add("Invalid operation type.");
                return (false, errors.ToArray());
            }
            
            var storedToken = await _redisCache.GetStringAsync($"user:{userId}:{_op}");

            var isValid = storedToken != null && storedToken == token;
            var action = isValid ? "user_validated" : "token_validation_failed";

            var authActivity = new AuthActivityEvent
            {
                UserId = userId,
                Action = action,
                Timestamp = DateTime.UtcNow
            };
            
            var meta = _userActionFactory.GetMetadata(UserActionConstants.Validation.LoginTokenValidation);

            var logEvent = new UserActivityEvent(
                userId: userId,
                action: meta.Action,
                category: meta.Category,
                description: meta.Description,
                defaultLogLevel: meta.DefaultLogLevel,
                timestamp: DateTime.UtcNow,
                metadata: null);
        
            await _messagePublisher.PublishAsync(QueueNames.LoggerActivity, logEvent);
            await _messagePublisher.PublishAsync(QueueNames.AuthActivity, authActivity);

            if (!isValid)
                errors.Add("Token does not match or has expired.");

            return (isValid, errors.ToArray());
        }
        
        public async Task HandleUserAuthenticationTokenAsync(Guid userId, string op)
        {
            var redisKey = $"user:{userId}:temp";
            var userDto = await _redisCache.GetAsync<UserDto>(redisKey);
            if (userDto == null) return;

            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.UserId, userId.ToString())
            };

            if (!string.IsNullOrWhiteSpace(userDto.UserName))
                claims.Add(new Claim(ClaimTypes.UserName, userDto.UserName));

            if (!string.IsNullOrWhiteSpace(userDto.Email))
                claims.Add(new Claim(ClaimTypes.Email, userDto.Email));
            
            claims.Add(!string.IsNullOrWhiteSpace(userDto.Role) ? new Claim(ClaimTypes.Role, userDto.Role) :
                new Claim(ClaimTypes.Role, "User"));
            
            var token = _generator.GenerateToken(claims);
            
            string _op = op switch
            {
                QueueNames.UserActionLogin => UserActionConstants.Validation.LoginTokenValidation,
                QueueNames.UserActionRegister => UserActionConstants.Validation.RegisteredTokenValidation,
                _ => "Error"
            };

            if (_op.Equals("Error", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }
            
            await _redisCache.SetStringAsync($"user:{userId}:{_op}", token, TimeSpan.FromHours(1));
            
            await PublishTokenAndActivityEvents(
                userId,
                token,
                "token_generated_for_registered_user",
                "user_authenticated"
            );
        }

        private async Task PublishTokenAndActivityEvents(Guid userId, string token, string authAction = "login_success", string userAction = "login")
        {
            var tokenEvent = new AuthTokenGeneratedEvent
            {
                UserId = userId,
                Token = token,
                GeneratedAt = DateTime.UtcNow
            };
            await _messagePublisher.PublishAsync(QueueNames.TokenGeneratedActivity, tokenEvent);

            var authActivity = new AuthActivityEvent
            {
                UserId = userId,
                Action = authAction,
                Timestamp = DateTime.UtcNow
            };
            await _messagePublisher.PublishAsync(QueueNames.AuthActivity, authActivity);

            var userActivity = new UserActivityEvent
            {
                UserId = userId,
                Action = userAction,
                Timestamp = DateTime.UtcNow
            };
            await _messagePublisher.PublishAsync(QueueNames.UserActivity, userActivity);
            
            var meta = _userActionFactory.GetMetadata(UserActionConstants.Authentication.TokenGenerated);

            var logEvent = new UserActivityEvent(
                userId: userId,
                action: meta.Action,
                category: meta.Category,
                description: meta.Description,
                defaultLogLevel: meta.DefaultLogLevel,
                timestamp: DateTime.UtcNow,
                metadata: null);
        
            await _messagePublisher.PublishAsync(QueueNames.LoggerActivity, logEvent);
        }
    }
}
