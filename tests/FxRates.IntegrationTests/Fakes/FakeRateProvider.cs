using System.Collections.Concurrent;
using FxRates.Application.Dtos;
using FxRates.Application.Exceptions;
using FxRates.Application.Interfaces;

namespace FxRates.IntegrationTests.Fakes;

/// <summary>Stands in for AlphaVantage: returns the rates a test sets and counts the calls.</summary>
public sealed class FakeRateProvider : IExchangeRateProvider
{
    private readonly ConcurrentDictionary<string, ExternalRate> _rates = new();
    private int _calls;

    public int Calls => _calls;

    /// <summary>When true, every call fails as if AlphaVantage were down.</summary>
    public bool IsUnavailable { get; set; }

    public void Set(string baseCurrency, string quoteCurrency, decimal bid, decimal ask) =>
        _rates[$"{baseCurrency}/{quoteCurrency}"] = new ExternalRate(bid, ask);

    public Task<ExternalRate?> GetRateAsync(string baseCurrency, string quoteCurrency, CancellationToken ct)
    {
        Interlocked.Increment(ref _calls);

        if (IsUnavailable)
            throw new ExternalProviderUnavailableException();

        return Task.FromResult(_rates.TryGetValue($"{baseCurrency}/{quoteCurrency}", out var rate) ? rate : null);
    }
}
