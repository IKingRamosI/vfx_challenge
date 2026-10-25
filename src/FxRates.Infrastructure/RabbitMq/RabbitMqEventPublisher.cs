using System.Text.Json;
using FxRates.Application.Interfaces;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace FxRates.Infrastructure.RabbitMq;

/// <summary>Publishes JSON messages to durable RabbitMQ queues. Registered as a singleton.</summary>
public sealed class RabbitMqEventPublisher(IOptions<RabbitMqOptions> options) : IEventPublisher, IDisposable
{
    private readonly SemaphoreSlim _lock = new(1, 1);
    private IConnection? _connection;

    public async Task PublishAsync<T>(string queue, T message, CancellationToken ct)
    {
        var connection = await GetConnectionAsync(ct);

        await using var channel = await connection.CreateChannelAsync(cancellationToken: ct);

        await channel.QueueDeclareAsync(queue, 
            durable: true, exclusive: false, autoDelete: false, cancellationToken: ct);

        var body = JsonSerializer.SerializeToUtf8Bytes(message);
        var props = new BasicProperties { Persistent = true, ContentType = "application/json" };

        await channel.BasicPublishAsync(exchange: string.Empty, 
            routingKey: queue, mandatory: false, basicProperties: props, body: body, 
            cancellationToken: ct);
    }

    private async Task<IConnection> GetConnectionAsync(CancellationToken ct)
    {
        if (_connection is { IsOpen: true }) 
            return _connection;

        await _lock.WaitAsync(ct);

        try
        {
            if (_connection is not { IsOpen: true })
            {
                var o = options.Value;
                var factory = new ConnectionFactory
                {
                    HostName = o.HostName,
                    Port = o.Port,
                    UserName = o.UserName,
                    Password = o.Password
                };
                _connection = await factory.CreateConnectionAsync(ct);
            }
            return _connection;
        }
        finally 
        { 
            _lock.Release(); 
        }
    }

    public void Dispose() => _connection?.Dispose();
}