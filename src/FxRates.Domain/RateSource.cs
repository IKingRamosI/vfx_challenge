namespace FxRates.Domain;

/// <summary>Where an exchange rate came from.</summary>
public enum RateSource
{
    /// <summary>Created or last updated through the API.</summary>
    Manual = 0,

    /// <summary>Fetched from the AlphaVantage API.</summary>
    AlphaVantage = 1
}
