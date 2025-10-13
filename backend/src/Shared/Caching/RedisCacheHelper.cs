using System.Text.Json;
using Shared.Helpers;
using StackExchange.Redis;

namespace Shared.Caching;

public class RedisCacheHelper
{
    private readonly IDatabase _db;
    private readonly RedisConnectionHelper _redisConnectionHelper;

    public RedisCacheHelper(RedisConnectionHelper redisConnectionHelper)
    {
        _redisConnectionHelper = redisConnectionHelper;
        _db = _redisConnectionHelper.GetDatabase();
    }
    public async Task SetAsync<T>(string key, T value, TimeSpan? expiry = null)
    {
        var json = JsonSerializer.Serialize(value);
        await _db.StringSetAsync(key, json, expiry);
    }
    
    public async Task SetStringAsync(string key, string value, TimeSpan? expiry = null)
    {
        await _db.StringSetAsync(key, value, expiry);
    }
    public async Task<T?> GetAsync<T>(string key)
    {
        var data = await _db.StringGetAsync(key);
        return data.HasValue ? JsonSerializer.Deserialize<T>(data!) : default;
    }
    
    public async Task<string?> GetStringAsync(string key)
    {
        var data = await _db.StringGetAsync(key);
        return data.HasValue ? data.ToString() : null;
    }
    
    public async Task<string?> WaitForValueAsync(
        string key,
        int maxAttempts = 5,
        int delayMs = 500)
    {
        string? value = null;

        for (int attempt = 1; attempt <= maxAttempts; attempt++)
        {
            value = await GetStringAsync(key);
            if (!string.IsNullOrEmpty(value))
                return value;

            await Task.Delay(delayMs);
        }
        return null;
    }
    public async Task RemoveAsync(string key) => await _db.KeyDeleteAsync(key);
}