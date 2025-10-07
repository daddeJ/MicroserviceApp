using StackExchange.Redis;
using System;

namespace Shared.Helpers
{
    public class RedisConnectionHelper
    {
        private static ConnectionMultiplexer? _redis;
        private static readonly object _lock = new();

        public ConnectionMultiplexer GetConnection(string? connectionString = null)
        {
            connectionString ??= Environment.GetEnvironmentVariable("REDIS_CONNECTION") ?? "redis:6379";

            if (_redis == null || !_redis.IsConnected)
            {
                lock (_lock)
                {
                    if (_redis == null || !_redis.IsConnected)
                    {
                        var options = ConfigurationOptions.Parse(connectionString);
                        options.AbortOnConnectFail = false; // Retry until Redis is available
                        options.ConnectRetry = 5;           // Number of retries
                        options.ConnectTimeout = 5000;      // Timeout per try
                        
                        _redis = ConnectionMultiplexer.Connect(options);
                        Console.WriteLine($"Connected to Redis: {connectionString}");
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
}