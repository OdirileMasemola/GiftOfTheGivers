using System.Globalization;
using System.Text.RegularExpressions;
using GiftOfTheGivers.Helpers;

namespace GiftOfTheGivers.Tests;

public class TaxCertificateFormatterTests
{
    [Fact]
    public void GenerateCertificateNumber_UsesDateAndEightCharacterSuffix()
    {
        var number = TaxCertificateFormatter.GenerateCertificateNumber(new DateTime(2026, 9, 25));

        Assert.Matches(new Regex("^CERT-20260925-[0-9A-F]{8}$"), number);
    }

    [Fact]
    public void FormatAmount_ShowsCurrencyAndTwoDecimals()
    {
        Assert.Equal("ZAR 1,250.00", TaxCertificateFormatter.FormatAmount(1250m));
        Assert.Equal("USD 10.50", TaxCertificateFormatter.FormatAmount(10.5m, "usd"));
    }

    [Fact]
    public void FormatAmount_IsTheSameWhenTheMachineUsesSouthAfricanSettings()
    {
        var original = CultureInfo.CurrentCulture;
        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("en-ZA");

            Assert.Equal("ZAR 300.00", TaxCertificateFormatter.FormatAmount(300m));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }
    }
}
