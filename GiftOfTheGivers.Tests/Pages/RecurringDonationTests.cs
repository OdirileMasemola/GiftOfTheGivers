using GiftOfTheGivers.Data;
using GiftOfTheGivers.Pages;
using GiftOfTheGivers.Pages.Dashboards;
using GiftOfTheGivers.Tests.TestSupport;
using GiftOfTheGivers.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Moq;

namespace GiftOfTheGivers.Tests.Pages;

/// <summary>
/// Recurring donations, cancelling a schedule and the printable tax certificate (added for the user manual).
/// </summary>
public class RecurringDonationTests
{
    [Theory]
    [InlineData(null, "Once")]
    [InlineData("monthly", "Monthly")]
    [InlineData(" Yearly ", "Yearly")]
    [InlineData("Daily", null)]
    [InlineData("Annually", null)]
    public void NormaliseFrequency_MatchesTheValuesTheDatabaseAccepts(string? input, string? expected)
    {
        Assert.Equal(expected, DonationRules.NormaliseFrequency(input));
    }

    [Fact]
    public async Task Post_SignedInDonorMonthly_RecordsFirstGiftAndActiveSchedule()
    {
        using var db = TestDb.Create();
        var donor = TestDb.AddUser(db, "lerato@example.com");
        var page = new DonateModel(db, new Mock<ILogger<DonateModel>>().Object)
        {
            Amount = 150m,
            Currency = "EUR",
            Frequency = "monthly"
        }.WithContext(PageModelSetup.SignedInAs(donor.UserId, "Donor"));

        var result = await page.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Single(db.Donations);
        var schedule = Assert.Single(db.DonationSchedules);
        Assert.Equal(donor.UserId, schedule.DonorId);
        Assert.Equal("Monthly", schedule.Frequency);
        Assert.Equal("EUR", schedule.Currency);
        Assert.Equal("Active", schedule.Status);
        Assert.Contains("recurring donation", (string)page.TempData["DonationMessage"]!);
    }

    [Fact]
    public async Task Post_GuestAsksForRecurring_IsRejectedAndNothingIsSaved()
    {
        using var db = TestDb.Create();
        var page = new DonateModel(db, new Mock<ILogger<DonateModel>>().Object)
        {
            Amount = 100m,
            Currency = "ZAR",
            Frequency = "Weekly"
        }.WithContext();

        var result = await page.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.True(page.ModelState.ContainsKey(nameof(DonateModel.Frequency)));
        Assert.Empty(db.Donations);
        Assert.Empty(db.DonationSchedules);
    }

    [Fact]
    public async Task CancelSchedule_OwnActiveSchedule_IsCancelled()
    {
        using var db = TestDb.Create();
        var donor = TestDb.AddUser(db, "donor1@example.com");
        var schedule = AddSchedule(db, donor);
        var page = new DonorModel(db, new Mock<ILogger<DonorModel>>().Object)
            .WithContext(PageModelSetup.SignedInAs(donor.UserId, "Donor"));

        await page.OnPostCancelScheduleAsync(schedule.DonationScheduleId);

        var saved = db.DonationSchedules.Single();
        Assert.Equal("Cancelled", saved.Status);
        Assert.Equal(DateTime.Today, saved.EndDate);
    }

    [Fact]
    public async Task CancelSchedule_SomeoneElsesSchedule_IsLeftAlone()
    {
        using var db = TestDb.Create();
        var owner = TestDb.AddUser(db, "owner@example.com");
        var other = TestDb.AddUser(db, "other@example.com");
        var schedule = AddSchedule(db, owner);
        var page = new DonorModel(db, new Mock<ILogger<DonorModel>>().Object)
            .WithContext(PageModelSetup.SignedInAs(other.UserId, "Donor"));

        await page.OnPostCancelScheduleAsync(schedule.DonationScheduleId);

        Assert.Equal("Active", db.DonationSchedules.Single().Status);
        Assert.Contains("not found", (string)page.TempData["DonorStatusMessage"]!);
    }

    [Fact]
    public async Task TaxCertificatePage_OwnDonationWithCertificate_ShowsIt()
    {
        using var db = TestDb.Create();
        var donor = TestDb.AddUser(db, "donor1@example.com");
        var donation = TestDb.AddDonation(db, donor, 400m);
        db.TaxCertificates.Add(new TaxCertificate
        {
            DonationId = donation.DonationId,
            CertificateNumber = "CERT-20261007-ABCDEF12",
            IssueDate = DateTime.Today,
            CertificateAmount = 400m,
            CreatedAt = DateTime.Now
        });
        db.SaveChanges();
        var page = new TaxCertificateModel(db).WithContext(PageModelSetup.SignedInAs(donor.UserId, "Donor"));

        var result = await page.OnGetAsync(donation.DonationId);

        Assert.IsType<PageResult>(result);
        Assert.Equal("CERT-20261007-ABCDEF12", page.Certificate!.CertificateNumber);
        Assert.Equal(donor.UserId, page.Donor!.UserId);
    }

    [Fact]
    public async Task TaxCertificatePage_SomeoneElsesDonation_ReturnsNotFound()
    {
        using var db = TestDb.Create();
        var owner = TestDb.AddUser(db, "owner@example.com");
        var other = TestDb.AddUser(db, "other@example.com");
        var donation = TestDb.AddDonation(db, owner, 50m);
        var page = new TaxCertificateModel(db).WithContext(PageModelSetup.SignedInAs(other.UserId, "Donor"));

        Assert.IsType<NotFoundResult>(await page.OnGetAsync(donation.DonationId));
    }

    private static DonationSchedule AddSchedule(ApplicationDbContext db, User donor)
    {
        var schedule = new DonationSchedule
        {
            DonorId = donor.UserId,
            Amount = 200m,
            Currency = "ZAR",
            Frequency = "Monthly",
            StartDate = DateTime.Today,
            Status = "Active",
            CreatedAt = DateTime.Now
        };
        db.DonationSchedules.Add(schedule);
        db.SaveChanges();
        return schedule;
    }
}
