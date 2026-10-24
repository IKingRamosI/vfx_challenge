using FxRates.Domain;

namespace FxRates.Application.Dtos;

/// <summary>An exchange rate as returned by the API.</summary>
public sealed record ExchangeRateResponse(
    int Id, 
    string BaseCurrency, 
    string QuoteCurrency, 
    decimal Bid, 
    decimal Ask, 
    string Source, 
    DateTime LastUpdatedUtc)
{
    public static ExchangeRateResponse From(ExchangeRate rate) => 
        new(
            rate.Id, 
            rate.BaseCurrency, 
            rate.QuoteCurrency, 
            rate.Bid, 
            rate.Ask, 
            rate.Source.ToString(), 
            rate.LastUpdatedUtc
            );
}