using AuthService.Messaging;
using Newtonsoft.Json;
using Shared.DTOs;
using Shared.Events;
using Shared.Helpers;

namespace AuthService.Services
{
    public class AuthServiceImp : IAuthService
    {
        private readonly JwtHelper _jwtHelper;
        private readonly RedisConnectionHelper _redisConnectionHelper;
        private readonly IEventPublisher _eventPublisher;

        private readonly Dictionary<string, string> _users = new()
        {
            { "testuser", "Password123!" }
        };

        public AuthServiceImp(
            JwtHelper jwtHelper,
            RedisConnectionHelper redisConnectionHelper,
            IEventPublisher eventPublisher)
        {
            _jwtHelper = jwtHelper;
            _redisConnectionHelper = redisConnectionHelper;
            _eventPublisher = eventPublisher;
        }
        
        public async Task<AuthResponseDto?> LoginAsync(AuthRequestDto dto)
        {
            if (!_users.TryGetValue(dto.Username, out var password) || password != dto.Password)
                return null;

            var userId = Guid.NewGuid();
            var token = _jwtHelper.GenerateToken(userId, dto.Username);

            var db = _redisConnectionHelper.GetDatabase();
            await db.StringSetAsync($"user:{userId}:token", token, TimeSpan.FromHours(1));

            var tokenEvent = new AuthTokenGeneratedEvent
            {
                UserId = userId,
                Token = token,
                GeneratedAt = DateTime.UtcNow
            };
            await _eventPublisher.PublishAsync("auth.token.generated", tokenEvent);

            var authActivity = new AuthActivityEvent
            {
                UserId = userId,
                Action = "login_success",
                Timestamp = DateTime.UtcNow
            };
            await _eventPublisher.PublishAsync("auth.activity", authActivity);

            var userActivity = new UserActivityEvent
            {
                UserId = userId,
                Action = "login",
                Timestamp = DateTime.UtcNow
            };
            await _eventPublisher.PublishAsync("user.activity", userActivity);

            return new AuthResponseDto
            {
                Token = token,
                Expires = DateTime.UtcNow.AddHours(1),
                RefreshToken = null
            };
        }

        public async Task HandleAuthTokenEventAsync(Guid userId, string token)
        {
            var db = _redisConnectionHelper.GetDatabase();
            var storedToken = await db.StringGetAsync($"user:{userId}:token");

            if (storedToken.HasValue && storedToken == token)
            {
                var authActivity = new AuthActivityEvent
                {
                    UserId = userId,
                    Action = "user_validated",
                    Timestamp = DateTime.UtcNow
                };
                await _eventPublisher.PublishAsync("auth.activity", authActivity);
            }
            else
            {
                var authActivity = new AuthActivityEvent
                {
                    UserId = userId,
                    Action = "token_validation_failed",
                    Timestamp = DateTime.UtcNow
                };
                await _eventPublisher.PublishAsync("auth.activity", authActivity);
            }
        }
        
        public async Task HandleUserRegisteredAsync(Guid userId)
        {
            var db = _redisConnectionHelper.GetDatabase();
            var redisKey = $"user:{userId}:temp";

            var userDataJson = await db.StringGetAsync(redisKey);
            if (!userDataJson.HasValue) return;

            var userDto = JsonConvert.DeserializeObject<UserRegistrationDto>(userDataJson);
            if (userDto == null) return;

            var token = _jwtHelper.GenerateToken(userId, userDto.Username);

            await db.StringSetAsync($"user:{userId}:token", token, TimeSpan.FromHours(1));

            var tokenEvent = new AuthTokenGeneratedEvent
            {
                UserId = userId,
                Token = token,
                GeneratedAt = DateTime.UtcNow
            };
            await _eventPublisher.PublishAsync("auth.token.generated", tokenEvent);

            var authActivity = new AuthActivityEvent
            {
                UserId = userId,
                Action = "token_generated_for_registered_user",
                Timestamp = DateTime.UtcNow
            };
            await _eventPublisher.PublishAsync("auth.activity", authActivity);

            var userActivity = new UserActivityEvent
            {
                UserId = userId,
                Action = "register_authenticated",
                Timestamp = DateTime.UtcNow
            };
            await _eventPublisher.PublishAsync("user.activity", userActivity);
        }
    }
}
