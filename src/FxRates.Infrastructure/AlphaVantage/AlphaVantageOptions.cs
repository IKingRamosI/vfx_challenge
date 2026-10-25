namespace FxRates.Infrastructure.AlphaVantage;

/// <summary>Settings bound from the "AlphaVantage" configuration section.</summary>
public sealed class AlphaVantageOptions
{
    public string BaseUrl { get; init; } = "https://www.alphavantage.co/";
    public string ApiKey { get; init; } = string.Empty;
}
