using GiftOfTheGivers.Data;
using GiftOfTheGivers.Validation;

namespace GiftOfTheGivers.Tests;

public class PasswordHashingTests
{
    [Fact]
    public void HashPassword_ThenVerify_Succeeds_ForSamePassword()
    {
        var hash = SeedData.HashPassword("Donor@123");

        Assert.False(string.IsNullOrWhiteSpace(hash));
        Assert.True(SeedData.VerifyPassword("Donor@123", hash));
    }

    [Fact]
    public void VerifyPassword_Fails_ForWrongPassword()
    {
        var hash = SeedData.HashPassword("Donor@123");

        Assert.False(SeedData.VerifyPassword("WrongPassword!", hash));
    }

    [Fact]
    public void VerifyPassword_ReturnsFalse_ForInvalidHash()
    {
        Assert.False(SeedData.VerifyPassword("anything", "not-a-valid-base64-hash!!!"));
    }
}

public class DonationRulesTests
{
    [Fact]
    public void IsValidAmount_AcceptsPositiveAmountWithinLimit()
    {
        Assert.True(DonationRules.IsValidAmount(250.50m, out var error));
        Assert.Null(error);
    }

    [Fact]
    public void IsValidAmount_RejectsNull()
    {
        Assert.False(DonationRules.IsValidAmount(null, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void IsValidAmount_RejectsZero()
    {
        Assert.False(DonationRules.IsValidAmount(0m, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void IsValidAmount_RejectsNegative()
    {
        Assert.False(DonationRules.IsValidAmount(-10m, out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void IsValidAmount_RejectsAboveMaximum()
    {
        Assert.False(DonationRules.IsValidAmount(DonationRules.MaxAmount + 1, out var error));
        Assert.Contains("at most", error!);
    }

    [Fact]
    public void IsValidCurrency_AcceptsZar()
    {
        Assert.True(DonationRules.IsValidCurrency("ZAR", out var error));
        Assert.Null(error);
    }

    [Fact]
    public void IsValidCurrency_AcceptsLowercaseUsd()
    {
        Assert.True(DonationRules.IsValidCurrency("usd", out var error));
        Assert.Null(error);
    }

    [Fact]
    public void IsValidCurrency_RejectsUnsupported()
    {
        Assert.False(DonationRules.IsValidCurrency("GBP", out var error));
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    [Fact]
    public void CanIssueTaxCertificate_OnlyForCompleted()
    {
        Assert.True(DonationRules.CanIssueTaxCertificate("Completed"));
        Assert.True(DonationRules.CanIssueTaxCertificate("completed"));
        Assert.False(DonationRules.CanIssueTaxCertificate("Pending"));
        Assert.False(DonationRules.CanIssueTaxCertificate("Failed"));
        Assert.False(DonationRules.CanIssueTaxCertificate(null));
    }
}
