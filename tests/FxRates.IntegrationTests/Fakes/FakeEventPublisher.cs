using System.Collections.Concurrent;
using FxRates.Application.Interfaces;

namespace FxRates.IntegrationTests.Fakes;

/// <summary>Stands in for RabbitMQ: keeps the published messages instead of sending them.</summary>
public sealed class FakeEventPublisher : IEventPublisher
{
    public ConcurrentQueue<object> Published { get; } = new();

    public Task PublishAsync<T>(string queue, T message, CancellationToken ct)
    {
        Published.Enqueue(message!);
        return Task.CompletedTask;
    }
}
