using System.Text.Json.Serialization;
using Newtonsoft.Json;
using Shared.DTOs;
using Shared.Events;
using Shared.Helpers;
using UserService.Messaging;

namespace UserService.Services;

public class UserServiceImp : IUserService
{
    private readonly RedisConnectionHelper _redisConnectionHelper;
    private readonly IEventPublisher _eventPublisher;
    public UserServiceImp(RedisConnectionHelper redisConnectionHelper, IEventPublisher eventPublisher)
    {
        _redisConnectionHelper = redisConnectionHelper;
        _eventPublisher = eventPublisher;
    }
    public async Task<UserRegistrationDto> RegistrationUserAsync(UserRegistrationDto dto)
    {
        var userId = Guid.NewGuid();
        dto.UserId = userId;

        var redisLey = $"user:{userId}:temp";
        var jsonData = JsonConvert.SerializeObject(dto);
        
        var db = _redisConnectionHelper.GetDatabase();
        await db.StringSetAsync(redisLey, jsonData, TimeSpan.FromHours(1));

        var activityEvent = new UserActivityEvent
        {
            UserId = userId,
            Action = "register",
            Timestamp = DateTime.UtcNow
        };
        await _eventPublisher.PublishAsync("user.registered", activityEvent);
        await _eventPublisher.PublishAsync("user.activity", activityEvent);

        return dto;
    }
}