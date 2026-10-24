namespace FxRates.Domain;

/// <summary>Validation and normalisation of ISO 4217 style currency codes.</summary>
public static class CurrencyCode
{
    /// <summary>
    /// Trims and upper-cases a currency code and checks it has exactly three ASCII letters.
    /// </summary>
    /// <param name="code">The code as received, e.g. " usd".</param>
    /// <returns>The normalised code, e.g. "USD".</returns>
    /// <exception cref="DomainValidationException">The code is missing or not three letters.</exception>
    public static string Normalize(string? code)
    {
        var value = code?.Trim().ToUpperInvariant();
        if (value is null || value.Length != 3 || !value.All(char.IsAsciiLetter))
            throw new DomainValidationException($"'{code}' is not a valid currency code (expected three letters, e.g. USD).");

        return value;
    }
}
