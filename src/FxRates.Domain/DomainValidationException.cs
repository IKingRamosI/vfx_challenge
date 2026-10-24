namespace FxRates.Domain;

/// <summary>Thrown when an operation would break a domain rule (mapped to HTTP 400).</summary>
public sealed class DomainValidationException(string message) : Exception(message);