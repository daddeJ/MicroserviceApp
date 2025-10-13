using Shared.Caching;
using Shared.DTOs;

namespace UserService.Services;

public class UserServiceImp : IUserService
{
    private readonly RedisCacheHelper _cacheHelper;
    private readonly IPublisherService _publisher;

    public UserServiceImp(RedisCacheHelper cacheHelper, IPublisherService publisher)
    {
        _cacheHelper = cacheHelper;
        _publisher = publisher;
    }

    public async Task<UserDto> AuthenticateUserAsync(UserDto dto, string activity)
    {
        var userId = dto.UserId == Guid.Empty ? Guid.NewGuid() : dto.UserId;
        var redisKey = $"user:{userId}:temp";
        await _cacheHelper.SetAsync(redisKey, dto, TimeSpan.FromHours(1));
        await _publisher.PublishTokenAndActivityEvents(userId, activity);
        return dto;
    }
}