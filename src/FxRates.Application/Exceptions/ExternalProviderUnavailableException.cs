namespace FxRates.Application.Exceptions;

/// <summary>The external provider failed or refused the request (HTTP 503).</summary>
public sealed class ExternalProviderUnavailableException(Exception? inner = null) 
    : Exception("The exchange rate provider is unavailable. Try again later.", inner);