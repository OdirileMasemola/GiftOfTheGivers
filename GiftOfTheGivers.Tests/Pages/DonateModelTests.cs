using System.Globalization;
using GiftOfTheGivers.Data;
using GiftOfTheGivers.Pages;
using GiftOfTheGivers.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Moq;

namespace GiftOfTheGivers.Tests.Pages;

public class DonateModelTests
{
    private readonly Mock<ILogger<DonateModel>> _logger = new();

    [Fact]
    public async Task Post_GuestDonation_SavesCompletedDonationAndIssuesCertificate()
    {
        using var db = TestDb.Create();
        var page = new DonateModel(db, _logger.Object) { Amount = 250m, Currency = "zar" }.WithContext();

        var result = await page.OnPostAsync();

        var redirect = Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("/Donate", redirect.PageName);

        var donation = Assert.Single(db.Donations.Include(d => d.User));
        Assert.Equal(250m, donation.Amount);
        Assert.Equal("ZAR", donation.Currency);
        Assert.Equal("Completed", donation.PaymentStatus);
        Assert.StartsWith("PAY-", donation.PaymentReference);
        Assert.Equal("anonymous@donor.local", donation.User!.Email);

        var certificate = Assert.Single(db.TaxCertificates);
        Assert.Equal(donation.DonationId, certificate.DonationId);
        Assert.Equal(250m, certificate.CertificateAmount);
        Assert.Matches("^CERT-\\d{8}-[0-9A-F]{8}$", certificate.CertificateNumber);
        Assert.Equal(true, page.TempData["DonationSuccess"]);
    }

    [Fact]
    public async Task Post_SignedInDonor_DonationIsLinkedToTheirAccount()
    {
        using var db = TestDb.Create();
        var donor = TestDb.AddUser(db, "thandi@example.com");
        var page = new DonateModel(db, _logger.Object) { Amount = 100m, Currency = "USD" }
            .WithContext(PageModelSetup.SignedInAs(donor.UserId, "Donor"));

        await page.OnPostAsync();

        var donation = Assert.Single(db.Donations);
        Assert.Equal(donor.UserId, donation.UserId);
        Assert.False(db.Users.Any(u => u.Email == "anonymous@donor.local"));
    }

    [Fact]
    public async Task Post_TwoGuestDonations_ShareOneGuestAccount()
    {
        using var db = TestDb.Create();

        await new DonateModel(db, _logger.Object) { Amount = 50m, Currency = "ZAR" }.WithContext().OnPostAsync();
        await new DonateModel(db, _logger.Object) { Amount = 75m, Currency = "ZAR" }.WithContext().OnPostAsync();

        Assert.Equal(2, db.Donations.Count());
        Assert.Equal(1, db.Users.Count(u => u.Email == "anonymous@donor.local"));
        Assert.Equal(2, db.TaxCertificates.Count());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("0")]
    [InlineData("-20")]
    [InlineData("1000000.01")]
    public async Task Post_InvalidAmount_ReturnsPageWithErrorAndSavesNothing(string? amount)
    {
        using var db = TestDb.Create();
        var page = new DonateModel(db, _logger.Object)
        {
            Amount = amount is null ? null : decimal.Parse(amount, CultureInfo.InvariantCulture),
            Currency = "ZAR"
        }.WithContext();

        var result = await page.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(page.ModelState.ContainsKey(nameof(DonateModel.Amount)));
        Assert.Empty(db.Donations);
    }

    [Fact]
    public async Task Post_UnsupportedCurrency_ReturnsPageWithCurrencyError()
    {
        using var db = TestDb.Create();
        var page = new DonateModel(db, _logger.Object) { Amount = 10m, Currency = "GBP" }.WithContext();

        var result = await page.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(page.ModelState.ContainsKey(nameof(DonateModel.Currency)));
        Assert.Empty(db.Donations);
    }

    [Fact]
    public async Task Post_SignedInIdWithNoMatchingUser_FallsBackToGuestAndLogsWarning()
    {
        using var db = TestDb.Create();
        var page = new DonateModel(db, _logger.Object) { Amount = 30m, Currency = "ZAR" }
            .WithContext(PageModelSetup.SignedInAs(9999, "Donor"));

        await page.OnPostAsync();

        var donation = Assert.Single(db.Donations.Include(d => d.User));
        Assert.Equal("anonymous@donor.local", donation.User!.Email);
        _logger.VerifyLogged(LogLevel.Warning, Times.Once());
    }

    [Fact]
    public async Task Post_WhenDatabaseSaveFails_ShowsFriendlyErrorAndLogsTheException()
    {
        // Mocked context: every save throws, as if Azure SQL were unreachable.
        var db = new Mock<ApplicationDbContext>(TestDb.NewOptions()) { CallBase = true };
        db.Setup(c => c.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateException("Database unavailable"));

        var page = new DonateModel(db.Object, _logger.Object) { Amount = 200m, Currency = "ZAR" }.WithContext();

        var result = await page.OnPostAsync();

        Assert.IsType<PageResult>(result);
        var error = Assert.Single(page.ModelState[string.Empty]!.Errors);
        Assert.Contains("could not record your donation", error.ErrorMessage);
        _logger.VerifyLogged(LogLevel.Error, Times.Once());
    }
}
