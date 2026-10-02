using GiftOfTheGivers.Validation;

namespace GiftOfTheGivers.Tests.Validation;

public class DonationRulesBoundaryTests
{
    [Theory]
    [InlineData(0.01)]
    [InlineData(1_000_000)]
    public void IsValidAmount_AcceptsTheSmallestAndLargestAllowedAmounts(double amount)
    {
        Assert.True(DonationRules.IsValidAmount((decimal)amount, out var error));
        Assert.Null(error);
    }

    [Theory]
    [InlineData(" eur ")]
    [InlineData("Usd")]
    public void IsValidCurrency_IgnoresCaseAndSurroundingSpaces(string currency)
    {
        Assert.True(DonationRules.IsValidCurrency(currency, out _));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsValidCurrency_RejectsMissingCurrency(string? currency)
    {
        Assert.False(DonationRules.IsValidCurrency(currency, out var error));
        Assert.Equal("Please select a valid currency (ZAR, USD, or EUR).", error);
    }
}
