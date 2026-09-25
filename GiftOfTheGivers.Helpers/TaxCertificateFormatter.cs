using System.Globalization;

namespace GiftOfTheGivers.Helpers;

/// <summary>
/// Reusable utilities for Gift of the Givers tax certificate numbers.
/// Preserves the certificate format used by the web app and Azure Function:
/// CERT-{yyyyMMdd}-{8-character uppercase GUID fragment}.
/// </summary>
public static class TaxCertificateFormatter
{
    /// <summary>
    /// Generates a new tax certificate number in the existing system format.
    /// Example: CERT-20260922-A1B2C3D4
    /// </summary>
    /// <param name="issuedOn">
    /// Optional date used for the yyyyMMdd segment.
    /// When omitted, <see cref="DateTime.Now"/> is used (same as Donate / GenerateTaxCertificate).
    /// </param>
    /// <returns>A certificate number string such as CERT-20260922-A1B2C3D4.</returns>
    public static string GenerateCertificateNumber(DateTime? issuedOn = null)
    {
        var stamp = issuedOn ?? DateTime.Now;
        var guidFragment = Guid.NewGuid().ToString()[..8].ToUpperInvariant();
        return $"CERT-{stamp:yyyyMMdd}-{guidFragment}";
    }

    /// <summary>
    /// Formats a certificate amount as "ZAR 1,250.00".
    /// Uses the invariant culture so the amount looks the same on a local machine set to
    /// en-ZA (which would otherwise show "1 250,00") and on the Azure App Service.
    /// </summary>
    /// <param name="amount">The donation or certificate amount.</param>
    /// <param name="currency">Currency code shown in front of the amount. Defaults to ZAR.</param>
    public static string FormatAmount(decimal amount, string currency = "ZAR")
    {
        var code = string.IsNullOrWhiteSpace(currency) ? "ZAR" : currency.Trim().ToUpperInvariant();
        return $"{code} {amount.ToString("#,##0.00", CultureInfo.InvariantCulture)}";
    }
}
