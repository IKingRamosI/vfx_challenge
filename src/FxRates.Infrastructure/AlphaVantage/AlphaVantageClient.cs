using System.Globalization;
using System.Text.Json;
using FxRates.Application.Dtos;
using FxRates.Application.Exceptions;
using FxRates.Application.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace FxRates.Infrastructure.AlphaVantage;

public sealed class AlphaVantageClient(HttpClient http,
    IOptions<AlphaVantageOptions> options,
    ILogger<AlphaVantageClient> logger) : IExchangeRateProvider
{
    public async Task<ExternalRate?> GetRateAsync(string baseCurrency, string quoteCurrency, CancellationToken ct)
    {
        var url = "query?function=CURRENCY_EXCHANGE_RATE" +
            $"&from_currency={Uri.EscapeDataString(baseCurrency)}" +
            $"&to_currency={Uri.EscapeDataString(quoteCurrency)}" +
            $"&apikey={Uri.EscapeDataString(options.Value.ApiKey)}";

        using var response = await SendAsync(url, ct);

        if (!response.IsSuccessStatusCode)
        {
            logger.LogWarning("AlphaVantage returned {StatusCode}", (int)response.StatusCode);
            throw new ExternalProviderUnavailableException();
        }

        await using var stream = await response.Content.ReadAsStreamAsync(ct);

        using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var root = json.RootElement;

        if (root.TryGetProperty("Realtime Currency Exchange Rate", out var data)
            && TryParse(data, "8. Bid Price", out var bid) && TryParse(data, "9. Ask Price", out var ask))
        {
            return new ExternalRate(bid, ask);
        }

        if (root.TryGetProperty("Error Message", out _))
        {
            logger.LogInformation("AlphaVantage does not know {Base}/{Quote}", baseCurrency, quoteCurrency);
            return null;
        }

        // "Note" / "Information": rate limit or key problem. Never log the API key.
        logger.LogWarning("AlphaVantage gave no rate for {Base}/{Quote}", baseCurrency, quoteCurrency);
        throw new ExternalProviderUnavailableException();
    }

    // Network error, timeout or open circuit breaker (after the resilience handler's retries):
    // the provider is unavailable (503). If the caller cancelled, let the cancellation through.
    private async Task<HttpResponseMessage> SendAsync(string url, CancellationToken ct)
    {
        try
        {
            return await http.GetAsync(url, ct);
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger.LogWarning(ex, "AlphaVantage request failed");
            throw new ExternalProviderUnavailableException(ex);
        }
    }

    private static bool TryParse(JsonElement data, string field, out decimal value) => 
        decimal.TryParse(data.TryGetProperty(field, out var p) ? p.GetString() : null, 
            NumberStyles.Float, CultureInfo.InvariantCulture, out value);
}