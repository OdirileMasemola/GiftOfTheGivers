using System.Text.RegularExpressions;
using GiftOfTheGivers.Helpers;

namespace GiftOfTheGivers.Tests.Helpers;

public class TaxCertificateFormatterEdgeCaseTests
{
    [Fact]
    public void GenerateCertificateNumber_WithoutDate_UsesToday()
    {
        var number = TaxCertificateFormatter.GenerateCertificateNumber();

        Assert.StartsWith($"CERT-{DateTime.Now:yyyyMMdd}-", number);
    }

    [Fact]
    public void GenerateCertificateNumber_GivesDifferentNumbersForTheSameDay()
    {
        var day = new DateTime(2026, 10, 2);

        var numbers = Enumerable.Range(0, 50)
            .Select(_ => TaxCertificateFormatter.GenerateCertificateNumber(day))
            .ToList();

        Assert.Equal(numbers.Count, numbers.Distinct().Count());
        Assert.All(numbers, n => Assert.Matches(new Regex("^CERT-20261002-[0-9A-F]{8}$"), n));
    }

    [Theory]
    [InlineData("", "ZAR 99.90")]
    [InlineData("  ", "ZAR 99.90")]
    [InlineData(" eur ", "EUR 99.90")]
    public void FormatAmount_BlankCurrencyDefaultsToZar(string currency, string expected)
    {
        Assert.Equal(expected, TaxCertificateFormatter.FormatAmount(99.9m, currency));
    }

    [Fact]
    public void FormatAmount_RoundsToTwoDecimalsAndGroupsThousands()
    {
        Assert.Equal("ZAR 1,000,000.00", TaxCertificateFormatter.FormatAmount(1_000_000m));
        Assert.Equal("ZAR 10.56", TaxCertificateFormatter.FormatAmount(10.555m));
    }
}
