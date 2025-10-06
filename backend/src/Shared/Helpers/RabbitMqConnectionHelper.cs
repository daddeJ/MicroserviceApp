using RabbitMQ.Client;
using System.Threading.Tasks;
using System;
using System.Threading;

namespace Shared.Helpers;

public class RabbitMqConnectionHelper
{
    private static IConnection? _connection;
    private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

    public async Task<IConnection> GetConnectionAsync(
        string hostName = "localhost",
        string userName = "guest",
        string password = "guest",
        int port = 5672)
    {
        if (_connection == null || !_connection.IsOpen)
        {
            await _semaphore.WaitAsync();
            try
            {
                if (_connection == null || !_connection.IsOpen)
                {
                    var factory = new ConnectionFactory()
                    {
                        HostName = hostName,
                        UserName = userName,
                        Password = password,
                        Port = port
                    };
                    _connection = await factory.CreateConnectionAsync();
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }
        return _connection;
    }

    public async Task<IChannel> GetChannelAsync()
    {
        var connection = await GetConnectionAsync();
        return await connection.CreateChannelAsync();
    }

    public async Task CloseConnectionAsync()
    {
        if (_connection != null && _connection.IsOpen)
        {
            await _connection.CloseAsync();
            _connection.Dispose();
        }
    }
}