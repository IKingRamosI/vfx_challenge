using FxRates.Application.Dtos;

namespace FxRates.Application.Interfaces;

/// <summary>Third-party source of exchange rates (AlphaVantage).</summary>
public interface IExchangeRateProvider
{
    /// <returns>The rate, or null when the provider does not know the pair.</returns>
    Task<ExternalRate?> GetRateAsync(string baseCurrency, string quoteCurrency, CancellationToken ct);
}
