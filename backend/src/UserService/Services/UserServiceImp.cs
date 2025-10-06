using Shared.DTOs;
using Shared.Events;
using Shared.Helpers;
using UserService.Messaging;

namespace UserService.Services;

public class UserServiceImp : IUserService
{
    private readonly RedisConnectionHelper _redisConnectionHelper;
    private readonly IEventPublisher _eventPublisher;
    
    private readonly Dictionary<Guid, UserRegistrationDto> _userRegistrations = new ();

    public UserServiceImp(RedisConnectionHelper redisConnectionHelper, IEventPublisher eventPublisher)
    {
        _redisConnectionHelper = redisConnectionHelper;
        _eventPublisher = eventPublisher;
    }
    public async Task<UserRegistrationDto> RegistrationUserAsync(UserRegistrationDto dto)
    {
        var userId = Guid.NewGuid();
        dto.UserId = userId;
        
        _userRegistrations[userId] = dto;

        var activityEvent = new UserActivityEvent
        {
            UserId = userId,
            ActivityType = "register",
            Timestamp = DateTime.UtcNow
        };
        
        await _eventPublisher.PublishAsync("user.activity", activityEvent);

        return dto;
    }

    public async Task HandleAuthTokenEventAsync(Guid userId, string token)
    {
        var db = _redisConnectionHelper.GetDatabase();
        var storedToken = await db.StringGetAsync($"user:{userId}:token");

        if (storedToken.HasValue && storedToken == token)
        {
            var activityEvent = new UserActivityEvent
            {
                UserId = userId,
                ActivityType = "auth_validated",
                Timestamp = DateTime.UtcNow
            };
            
            await _eventPublisher.PublishAsync("user.activity", activityEvent);
        }
    }
}