namespace FxRates.Application.Interfaces;

/// <summary>Publishes integration events to a message queue.</summary>
public interface IEventPublisher
{
    Task PublishAsync<T>(string queue, T message, CancellationToken ct);
}