using StackExchange.Redis;

namespace Shared.Helpers;

public class RedisConnectionHelper
{
    private static ConnectionMultiplexer? _redis;
    private static readonly object _lock = new();

    public ConnectionMultiplexer GetConnection(string connectionString = "localhost:6379")
    {
        if (_redis == null || !_redis.IsConnected)
        {
            lock (_lock)
            {
                if (_redis == null || !_redis.IsConnected)
                {
                    _redis = ConnectionMultiplexer.Connect(connectionString);
                    Console.WriteLine($"Connected to redis: {connectionString}");
                }
            }
        }
        return _redis;
    }

    public IDatabase GetDatabase(int db = 0)
    {
        var connection = GetConnection();
        return connection.GetDatabase(db);
    }

    public void CloseConnection()
    {
        if (_redis != null && _redis.IsConnected)
        {
            _redis.Close();
            _redis.Dispose();
            Console.WriteLine("Redis connection closed");
        }
    }
}