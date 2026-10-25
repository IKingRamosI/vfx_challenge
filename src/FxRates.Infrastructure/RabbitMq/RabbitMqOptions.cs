namespace FxRates.Infrastructure.RabbitMq;

/// <summary>Settings bound from the "RabbitMq" configuration section.</summary>
public sealed class RabbitMqOptions
{
    public string HostName { get; init; } = "localhost";
    public int Port { get; init; } = 5672;
    public string UserName { get; init; } = "guest";
    public string Password { get; init; } = "guest";
}