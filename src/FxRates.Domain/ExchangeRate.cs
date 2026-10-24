namespace FxRates.Domain;

/// <summary>Bid and ask prices for one currency pair, e.g. USD/EUR.</summary>
public sealed class ExchangeRate
{
    public int Id { get; private set; }
    public string BaseCurrency { get; private set; } = default!;
    public string QuoteCurrency { get; private set; } = default!;
    public decimal Bid { get; private set; }
    public decimal Ask { get; private set; }
    public RateSource Source { get; private set; }
    public DateTime LastUpdatedUtc { get; private set; }

    private ExchangeRate() { } // used by EF Core

    /// <summary>Creates a valid rate or throws <see cref="DomainValidationException"/>.</summary>
    public static ExchangeRate Create(string baseCurrency, string quoteCurrency, 
        decimal bid, decimal ask, RateSource source)
    {
        var bCurrency = CurrencyCode.Normalize(baseCurrency);
        var qCurrency = CurrencyCode.Normalize(quoteCurrency);

        if (bCurrency == qCurrency)
            throw new DomainValidationException("Base and quote currencies must be different.");

        ValidatePrices(bid, ask);

        return new ExchangeRate
        {
            BaseCurrency = bCurrency,
            QuoteCurrency = qCurrency,
            Bid = bid,
            Ask = ask,
            Source = source,
            LastUpdatedUtc = DateTime.UtcNow,
        };
    }

    /// <summary>Replaces both prices; a manual update makes the source Manual.</summary>
    public void UpdatePrices(decimal bid, decimal ask, RateSource source)
    {
        ValidatePrices(bid, ask);
        Bid = bid;
        Ask = ask;
        Source = source;
        LastUpdatedUtc = DateTime.UtcNow;
    }

    private static void ValidatePrices(decimal bid, decimal ask)
    {
        if (bid <= 0 || ask <= 0)
            throw new DomainValidationException("Bid and ask must be greater than zero.");
        if (bid > ask)
            throw new DomainValidationException("Bid cannot be greater than ask.");
    }
}
