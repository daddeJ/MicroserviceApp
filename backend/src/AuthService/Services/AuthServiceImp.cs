using AuthService.Messaging;
using Shared.DTOs;
using Shared.Events;
using Shared.Helpers;

namespace AuthService.Services;

public class AuthServiceImp : IAuthService
{
    private readonly JwtHelper _jwtHelper;
    private readonly RedisConnectionHelper _redisConnectionHelper;
    private readonly IEventPublisher _eventPublisher;

    private readonly Dictionary<string, string> _users = new()
    {
        { "testuser", "Password123!" }
    };

    public AuthServiceImp(JwtHelper jwtHelper, RedisConnectionHelper redisConnectionHelper, IEventPublisher eventPublisher)
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

        var authEvent = new AuthTokenGeneratedEvent
        {
            UserId = userId,
            Token = token,
            GeneratedAt = DateTime.UtcNow.AddHours(1)
        };

        var activityEvent = new UserActivityEvent
        {
            UserId = userId,
            ActivityType = "login",
            Timestamp = DateTime.UtcNow,
        };
        
        await _eventPublisher.PublishAsync("auth.token.generated", authEvent);
        await _eventPublisher.PublishAsync("auth.activity", activityEvent);

        return new AuthResponseDto
        {
            Token = token,
            Expires = DateTime.UtcNow.AddHours(1),
            RefreshToken = null
        };
    }
}