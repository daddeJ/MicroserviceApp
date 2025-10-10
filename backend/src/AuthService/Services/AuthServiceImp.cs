using System.Security.Claims;
using AuthService.Messaging;
using Newtonsoft.Json;
using Shared.Caching;
using Shared.Constants;
using Shared.DTOs;
using Shared.Events;
using Shared.Helpers;
using Shared.Interfaces;
using Shared.Security;
using ClaimTypes = Shared.Constants.ClaimTypes;
using JsonSerializerOptions = System.Text.Json.JsonSerializerOptions;

namespace AuthService.Services
{
    public class AuthServiceImp : IAuthService
    {
        private readonly JwtTokenGenerator _generator;
        private readonly RedisCacheHelper _redisCache;
        private readonly IMessagePublisher _messagePublisher;

        public AuthServiceImp(
            JwtTokenGenerator generator,
            RedisCacheHelper redisCache,
            IMessagePublisher messagePublisher)
        {
            _generator = generator;
            _redisCache = redisCache;
            _messagePublisher = messagePublisher;
        }
        
        public async Task HandleAuthTokenEventAsync(Guid userId, string token)
        {
            var storedToken = await _redisCache.GetAsync<string>($"user:{userId}:token");

            var isValid = storedToken != null && storedToken == token;
            var action = isValid ? "user_validated" : "token_validation_failed";

            var authActivity = new AuthActivityEvent
            {
                UserId = userId,
                Action = action,
                Timestamp = DateTime.UtcNow
            };

            await _messagePublisher.PublishAsync(QueueNames.AuthActivity, authActivity);
        }
        
        public async Task HandleUserAuthenticationTokenAsync(Guid userId)
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
            
            await _redisCache.SetStringAsync($"user:{userId}:token", token, TimeSpan.FromHours(1));
            
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
        }
    }
}
