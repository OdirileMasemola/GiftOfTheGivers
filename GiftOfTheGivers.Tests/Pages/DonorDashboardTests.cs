using GiftOfTheGivers.Data;
using GiftOfTheGivers.Pages.Dashboards;
using GiftOfTheGivers.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Moq;

namespace GiftOfTheGivers.Tests.Pages;

public class DonorDashboardTests
{
    private readonly Mock<ILogger<DonorModel>> _logger = new();

    [Fact]
    public async Task Get_CalculatesTotalsFromCompletedDonationsOnly()
    {
        using var db = TestDb.Create();
        var donor = TestDb.AddUser(db, "donor1@example.com");
        var someoneElse = TestDb.AddUser(db, "donor2@example.com");

        TestDb.AddDonation(db, donor, 100m);
        TestDb.AddDonation(db, donor, 250.50m);
        TestDb.AddDonation(db, donor, 20m, currency: "USD");
        TestDb.AddDonation(db, donor, 999m, status: "Pending");
        TestDb.AddDonation(db, someoneElse, 5000m);

        db.DonationSchedules.AddRange(
            new DonationSchedule { DonorId = donor.UserId, Amount = 300m, Currency = "ZAR", Frequency = "Monthly", Status = "Active", StartDate = DateTime.Today, CreatedAt = DateTime.Now },
            new DonationSchedule { DonorId = donor.UserId, Amount = 50m, Currency = "ZAR", Frequency = "Weekly", Status = "Paused", StartDate = DateTime.Today, CreatedAt = DateTime.Now });
        db.SaveChanges();

        var page = new DonorModel(db, _logger.Object).WithContext(PageModelSetup.SignedInAs(donor.UserId, "Donor"));

        var result = await page.OnGetAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal(350.50m, page.TotalCompletedZar);
        Assert.Equal(20m, page.TotalsByCurrency["USD"]);
        Assert.Equal(3, page.CompletedDonationCount);
        Assert.Equal(4, page.Donations.Count);
        Assert.Equal(2, page.Schedules.Count);
        Assert.Equal(1, page.ActiveScheduleCount);
    }

    [Fact]
    public async Task Get_WithoutSignedInUser_ReturnsChallenge()
    {
        using var db = TestDb.Create();
        var page = new DonorModel(db, _logger.Object).WithContext();

        Assert.IsType<ChallengeResult>(await page.OnGetAsync());
    }

    [Fact]
    public async Task RequestCertificate_ForCompletedDonation_CreatesItOnce()
    {
        using var db = TestDb.Create();
        var donor = TestDb.AddUser(db, "donor1@example.com");
        var donation = TestDb.AddDonation(db, donor, 400m);
        var user = PageModelSetup.SignedInAs(donor.UserId, "Donor");

        await new DonorModel(db, _logger.Object).WithContext(user).OnPostRequestCertificateAsync(donation.DonationId);
        var second = new DonorModel(db, _logger.Object).WithContext(user);
        await second.OnPostRequestCertificateAsync(donation.DonationId);

        var certificate = Assert.Single(db.TaxCertificates);
        Assert.Equal(400m, certificate.CertificateAmount);
        Assert.Contains("already available", (string)second.TempData["DonorStatusMessage"]!);
    }

    [Fact]
    public async Task RequestCertificate_ForAnotherDonorsDonation_IsRefused()
    {
        using var db = TestDb.Create();
        var donor = TestDb.AddUser(db, "donor1@example.com");
        var owner = TestDb.AddUser(db, "donor2@example.com");
        var donation = TestDb.AddDonation(db, owner, 400m);
        var page = new DonorModel(db, _logger.Object).WithContext(PageModelSetup.SignedInAs(donor.UserId, "Donor"));

        await page.OnPostRequestCertificateAsync(donation.DonationId);

        Assert.Empty(db.TaxCertificates);
        Assert.Contains("not found on your account", (string)page.TempData["DonorStatusMessage"]!);
    }

    [Fact]
    public async Task RequestCertificate_ForPendingDonation_IsRefused()
    {
        using var db = TestDb.Create();
        var donor = TestDb.AddUser(db, "donor1@example.com");
        var donation = TestDb.AddDonation(db, donor, 400m, status: "Pending");
        var page = new DonorModel(db, _logger.Object).WithContext(PageModelSetup.SignedInAs(donor.UserId, "Donor"));

        await page.OnPostRequestCertificateAsync(donation.DonationId);

        Assert.Empty(db.TaxCertificates);
        Assert.Contains("only available for completed donations", (string)page.TempData["DonorStatusMessage"]!);
    }
}
