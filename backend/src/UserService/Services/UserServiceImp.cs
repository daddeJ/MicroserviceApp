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

    public async Task<UserDto> RegistrationUserAsync(UserDto dto)
    {
        var userId = dto.UserId == Guid.Empty ? Guid.NewGuid() : dto.UserId;

        var redisKey = $"user:{userId}:temp";

        await _cacheHelper.SetAsync(redisKey, dto, TimeSpan.FromHours(1));

        var activityEvent = new UserActivityEvent
        {
            UserId = userId,
            Action = "register",
            Timestamp = DateTime.UtcNow
        };

        await _messagePublisher.PublishAsync(QueueNames.UserRegisterActivity, activityEvent);
        await _messagePublisher.PublishAsync(QueueNames.UserActivity, activityEvent);

        return dto;
    }
}