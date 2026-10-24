using FxRates.Application.Dtos;
using FxRates.Application.Events;
using FxRates.Application.Exceptions;
using FxRates.Application.Interfaces;
using FxRates.Domain;
using Microsoft.Extensions.Logging;

namespace FxRates.Application;

public sealed class ExchangeRateService(
    IExchangeRateRepository repository,
    IExchangeRateProvider provider,
    IEventPublisher publisher,
    ILogger<ExchangeRateService> logger) : IExchangeRateService
{
    public const string RateCreatedQueue = "rates.created";

    public async Task<ExchangeRateResponse> CreateAsync(CreateRateRequest request, CancellationToken ct)
    {
        var rate = ExchangeRate.Create(request.BaseCurrency, request.QuoteCurrency, request.Bid, request.Ask, RateSource.Manual);

        if (await repository.ExistsAsync(rate.BaseCurrency, rate.QuoteCurrency, ct))
            throw new RateAlreadyExistsException(rate.BaseCurrency, rate.QuoteCurrency);

        await repository.AddAsync(rate, ct);
        await repository.SaveChangesAsync(ct);

        logger.LogInformation("Rate {RateId} {Base}/{Quote} created manually", rate.Id, rate.BaseCurrency, rate.QuoteCurrency);

        await PublishCreatedAsync(rate, ct);

        return ExchangeRateResponse.From(rate);
    }

    public async Task<bool> DeleteAsync(int id, CancellationToken ct)
    {
        var rate = await repository.GetByIdAsync(id, ct);

        if (rate is null)
            return false;

        repository.Remove(rate);
        await repository.SaveChangesAsync(ct);

        logger.LogInformation("Rate {RateId} {Base}/{Quote} deleted", rate.Id, rate.BaseCurrency, rate.QuoteCurrency);

        return true;
    }

    public async Task<IReadOnlyList<ExchangeRateResponse>> GetAllAsync(CancellationToken ct)
    {
        var rates = await repository.GetAllAsync(ct);
        return rates.Select(ExchangeRateResponse.From).ToList();
    }

    public async Task<ExchangeRateResponse?> GetByIdAsync(int id, CancellationToken ct)
    {
        var rate = await repository.GetByIdAsync(id, ct);

        if (rate is null)
            return null;

        return ExchangeRateResponse.From(rate);
    }

    /// <summary>Returns the stored rate, or fetches, stores and announces it.</summary>
    public async Task<ExchangeRateResponse?> GetByPairAsync(string baseCurrency, 
        string quoteCurrency, CancellationToken ct)
    {
        var b = CurrencyCode.Normalize(baseCurrency);
        var q = CurrencyCode.Normalize(quoteCurrency);

        var existing = await repository.GetByPairAsync(b, q, ct);

        if (existing is not null)
        {
            logger.LogInformation("Rate {Base}/{Quote} served from the database", b, q);
            return ExchangeRateResponse.From(existing);
        }

        logger.LogInformation("Rate {Base}/{Quote} not stored; fetching from provider", b, q);

        var external = await provider.GetRateAsync(b, q, ct);

        if (external is null)
            return null; // the controller turns this into 404

        ExchangeRate rate;
        try
        {
            rate = ExchangeRate.Create(b, q, external.Bid, external.Ask, RateSource.AlphaVantage);
        }
        catch (DomainValidationException ex)
        {
            throw new ExternalProviderUnavailableException(ex);   // provider sent invalid data -> 503
        }

        await repository.AddAsync(rate, ct);
        await repository.SaveChangesAsync(ct);

        logger.LogInformation("Rate {RateId} {Base}/{Quote} stored from AlphaVantage", rate.Id, b, q);

        await PublishCreatedAsync(rate, ct);

        return ExchangeRateResponse.From(rate);
    }

    public async Task<ExchangeRateResponse?> UpdateAsync(int id, UpdateRateRequest request, CancellationToken ct)
    {
        var rate = await repository.GetByIdAsync(id, ct);

        if (rate is null)
            return null;

        rate.UpdatePrices(request.Bid, request.Ask, RateSource.Manual);
        await repository.SaveChangesAsync(ct);

        logger.LogInformation("Rate {RateId} updated: bid {Bid}, ask {Ask}", rate.Id, rate.Bid, rate.Ask);

        return ExchangeRateResponse.From(rate);
    }

    // The rate is already saved: a broker failure is logged, not returned to the client.
    // Known limitation, fixed properly by the Outbox pattern (see README).
    private async Task PublishCreatedAsync(ExchangeRate rate, CancellationToken ct)
    {
        try
        {
            await publisher.PublishAsync(RateCreatedQueue, 
                new RateCreatedEvent(rate.Id, rate.BaseCurrency, rate.QuoteCurrency, 
                rate.Bid, rate.Ask, rate.Source.ToString(), DateTime.UtcNow), ct);

            logger.LogInformation("Published RateCreated for rate {RateId} to {Queue}", rate.Id, RateCreatedQueue);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to publish RateCreated for rate {RateId}", rate.Id);
        }
    }    
}