using Microsoft.JSInterop.Infrastructure;
using Shared.Caching;
using Shared.Constants;
using Shared.DTOs;
using Shared.Events;
using Shared.Interfaces;
using UserService.Messaging;

namespace UserService.Services;

public class UserServiceImp : IUserService
{
    private readonly RedisCacheHelper _cacheHelper;
    private readonly IMessagePublisher _messagePublisher;

    public UserServiceImp(RedisCacheHelper cacheHelper, IMessagePublisher messagePublisher)
    {
        _cacheHelper = cacheHelper;
        _messagePublisher = messagePublisher;
    }

    public async Task<UserDto> AuthenticateUserAsync(UserDto dto, string activity)
    {
        var userId = dto.UserId == Guid.Empty ? Guid.NewGuid() : dto.UserId;
        var redisKey = $"user:{userId}:temp";
        await _cacheHelper.SetAsync(redisKey, dto, TimeSpan.FromHours(1));
        await PublishTokenAndActivityEvents(userId, activity);
        return dto;
    }
    
    private async Task PublishTokenAndActivityEvents(Guid userId, string userAction)
    {
        var authActivity = new AuthActivityEvent()
        {
            UserId = userId,
            Action = "generate_token",
            Timestamp = DateTime.UtcNow
        };
        await _messagePublisher.PublishAsync(QueueNames.GenerateTokenActivity, authActivity);

        var userActivity = new UserActivityEvent
        {
            UserId = userId,
            Action = userAction,
            Timestamp = DateTime.UtcNow
        };
        await _messagePublisher.PublishAsync(QueueNames.UserActivity, userActivity);
    }

}