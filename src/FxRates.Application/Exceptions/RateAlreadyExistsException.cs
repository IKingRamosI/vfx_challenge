namespace FxRates.Application.Exceptions;

/// <summary>A rate for the pair already exists (HTTP 409).</summary>
public sealed class RateAlreadyExistsException(string baseCurrency, string quoteCurrency) 
    : Exception($"A rate for {baseCurrency}/{quoteCurrency} already exists.");