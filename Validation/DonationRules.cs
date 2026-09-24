namespace GiftOfTheGivers.Validation;

/// <summary>
/// Donation input rules used by the public Donate page.
/// Kept as pure static helpers so CI can verify the same business rules.
/// </summary>
public static class DonationRules
{
    public const decimal MaxAmount = 1_000_000m;
    public static readonly string[] AllowedCurrencies = ["ZAR", "USD", "EUR"];

    public static bool IsValidAmount(decimal? amount, out string? error)
    {
        if (amount is null)
        {
            error = "Please enter a donation amount.";
            return false;
        }

        if (amount <= 0)
        {
            error = "Amount must be greater than 0.";
            return false;
        }

        if (amount > MaxAmount)
        {
            error = $"Amount must be at most {MaxAmount:N0}.";
            return false;
        }

        error = null;
        return true;
    }

    public static bool IsValidCurrency(string? currency, out string? error)
    {
        if (string.IsNullOrWhiteSpace(currency) ||
            !AllowedCurrencies.Contains(currency.Trim().ToUpperInvariant()))
        {
            error = "Please select a valid currency (ZAR, USD, or EUR).";
            return false;
        }

        error = null;
        return true;
    }

    /// <summary>
    /// Mirrors tax-certificate eligibility used by Donate and GenerateTaxCertificate.
    /// </summary>
    public static bool CanIssueTaxCertificate(string? paymentStatus) =>
        string.Equals(paymentStatus, "Completed", StringComparison.OrdinalIgnoreCase);
}
