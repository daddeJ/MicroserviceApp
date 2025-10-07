using RabbitMQ.Client;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Shared.Helpers
{
    public class RabbitMqConnectionHelper
    {
        private static IConnection? _connection;
        private static readonly SemaphoreSlim _semaphore = new(1, 1);

        private string GetEnv(string key, string defaultValue) =>
            Environment.GetEnvironmentVariable(key) ?? defaultValue;

        public async Task<IConnection> GetConnectionAsync(
            string? hostName = null,
            string? userName = null,
            string? password = null,
            int port = 5672,
            int maxRetries = 10,
            int delayMilliseconds = 5000)
        {
            hostName ??= GetEnv("RABBITMQ_HOST", "rabbitmq");
            userName ??= GetEnv("RABBITMQ_USER", "guest");
            password ??= GetEnv("RABBITMQ_PASS", "guest");

            if (_connection != null && _connection.IsOpen)
                return _connection;

            await _semaphore.WaitAsync();
            try
            {
                if (_connection != null && _connection.IsOpen)
                    return _connection;

                int attempt = 0;
                while (attempt < maxRetries)
                {
                    try
                    {
                        var factory = new ConnectionFactory
                        {
                            HostName = hostName,
                            UserName = userName,
                            Password = password,
                            Port = port
                        };

                        _connection = await factory.CreateConnectionAsync();
                        Console.WriteLine($"[RabbitMQ] Connected to {hostName}:{port}");
                        return _connection;
                    }
                    catch (Exception ex)
                    {
                        attempt++;
                        Console.WriteLine($"[RabbitMQ] Connection attempt {attempt} failed: {ex.Message}");
                        await Task.Delay(delayMilliseconds);
                    }
                }

                throw new Exception($"Could not connect to RabbitMQ at {hostName}:{port} after {maxRetries} attempts.");
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<IChannel> GetChannelAsync()
        {
            var connection = await GetConnectionAsync();
            return await connection.CreateChannelAsync(); // IChannel for RabbitMQ.Client v8+
        }

        public async Task CloseConnectionAsync()
        {
            if (_connection != null && _connection.IsOpen)
            {
                await _connection.CloseAsync();
                _connection.Dispose();
                _connection = null;
                Console.WriteLine("[RabbitMQ] Connection closed");
            }
        }
    }
}
