using System.Net;
using GiftOfTheGivers.Data;
using GiftOfTheGivers.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace GiftOfTheGivers.IntegrationTests;

/// <summary>
/// Donor journeys through the running app: sign in, donate, get a tax certificate.
/// </summary>
public class DonorWorkflowTests : IClassFixture<GiftOfTheGiversWebFactory>
{
    private readonly GiftOfTheGiversWebFactory _factory;

    public DonorWorkflowTests(GiftOfTheGiversWebFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _factory.Output = output;
    }

    [Fact]
    public async Task SignedInDonor_Donates_DonationAndCertificateAreSaved_AndFunctionReturnsTheSameCertificate()
    {
        var browser = new BrowserSession(_factory.CreateBrowserClient());
        await browser.SignInAsync(DemoUsers.DonorEmail, DemoUsers.DonorPassword);

        // 1. Submit the donate form.
        var response = await browser.SubmitFormAsync("/Donate", new Dictionary<string, string>
        {
            ["Amount"] = "750",
            ["Currency"] = "ZAR"
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/Donate", BrowserSession.PathOf(response));
        Assert.Contains("Donation recorded:", await browser.GetPageAsync("/Donate"));

        // 2. The donation and its certificate are in the database, linked to the donor.
        var (donation, certificate) = await _factory.WithDbAsync(async db =>
        {
            var saved = await db.Donations.Include(d => d.User)
                .Where(d => d.User!.Email == DemoUsers.DonorEmail && d.Amount == 750m)
                .SingleAsync();
            var cert = await db.TaxCertificates.SingleAsync(tc => tc.DonationId == saved.DonationId);
            return (saved, cert);
        });

        Assert.Equal("Completed", donation.PaymentStatus);
        Assert.Equal("ZAR", donation.Currency);
        Assert.StartsWith("PAY-", donation.PaymentReference);
        Assert.Equal(750m, certificate.CertificateAmount);

        // 3. The GenerateTaxCertificate function sees the same donation and does not create a duplicate.
        var (status, body) = await TaxCertificateFunctionRunner.RunAsync(_factory, donation.DonationId);

        Assert.Equal(200, status);
        Assert.True(body.GetProperty("alreadyExisted").GetBoolean());
        Assert.Equal(certificate.CertificateNumber, body.GetProperty("certificateNumber").GetString());

        // 4. The donor sees the certificate on their dashboard.
        Assert.Contains(certificate.CertificateNumber, await browser.GetPageAsync("/Dashboards/Donor"));
    }

    [Fact]
    public async Task AnonymousDonations_AreRecordedAgainstOneGuestAccount()
    {
        var browser = new BrowserSession(_factory.CreateBrowserClient());

        foreach (var amount in new[] { "120", "80" })
        {
            var response = await browser.SubmitFormAsync("/Donate", new Dictionary<string, string>
            {
                ["Amount"] = amount,
                ["Currency"] = "USD"
            });
            Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        }

        var guestDonations = await _factory.WithDbAsync(db => db.Donations
            .Include(d => d.User)
            .Where(d => d.User!.Email == "anonymous@donor.local" && d.Currency == "USD")
            .ToListAsync());

        Assert.Equal(2, guestDonations.Count);
        Assert.Equal(1, await _factory.WithDbAsync(db => db.Users.CountAsync(u => u.Email == "anonymous@donor.local")));
    }

    [Fact]
    public async Task RecurringSchedule_ShowsOnDonorDashboardAndEmployeeReport()
    {
        // The app has no form to create schedules yet, so the schedule is added straight to the database.
        await _factory.WithDbAsync(async db =>
        {
            var donor = await db.Users.SingleAsync(u => u.Email == DemoUsers.DonorEmail);
            db.DonationSchedules.Add(new DonationSchedule
            {
                DonorId = donor.UserId,
                Amount = 500m,
                Currency = "ZAR",
                Frequency = "Monthly",
                Status = "Active",
                StartDate = new DateTime(2026, 10, 1),
                CreatedAt = DateTime.Now
            });
            await db.SaveChangesAsync();
        });

        var donor = new BrowserSession(_factory.CreateBrowserClient());
        await donor.SignInAsync(DemoUsers.DonorEmail, DemoUsers.DonorPassword);
        var dashboard = await donor.GetPageAsync("/Dashboards/Donor");

        Assert.Contains("Recurring schedules", dashboard);
        Assert.Contains("Monthly", dashboard);
        Assert.Contains("2026-10-01", dashboard);

        var employee = new BrowserSession(_factory.CreateBrowserClient());
        await employee.SignInAsync(DemoUsers.EmployeeEmail, DemoUsers.EmployeePassword);
        var report = await employee.GetPageAsync("/Dashboards/Donations");

        Assert.Contains("Donor Demo", report);
        Assert.Contains("Monthly", report);
    }

    [Theory]
    [InlineData("0", "ZAR", "Amount must be greater than 0")]
    [InlineData("-50", "ZAR", "Amount must be greater than 0")]
    [InlineData("1000001", "ZAR", "at most 1,000,000")]
    [InlineData("100", "GBP", "Please select a valid currency")]
    public async Task InvalidDonation_IsRejectedWithAMessage_AndNothingIsSaved(string amount, string currency, string expectedMessage)
    {
        var before = await _factory.WithDbAsync(db => db.Donations.CountAsync());
        var browser = new BrowserSession(_factory.CreateBrowserClient());

        var response = await browser.SubmitFormAsync("/Donate", new Dictionary<string, string>
        {
            ["Amount"] = amount,
            ["Currency"] = currency
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(expectedMessage, WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync()));
        Assert.Equal(before, await _factory.WithDbAsync(db => db.Donations.CountAsync()));
    }

    [Fact]
    public async Task TaxCertificateFunction_RejectsMissingUnknownAndPendingDonations()
    {
        var pendingId = await _factory.WithDbAsync(async db =>
        {
            var donor = await db.Users.SingleAsync(u => u.Email == DemoUsers.DonorEmail);
            var pending = new Donation
            {
                UserId = donor.UserId,
                Amount = 60m,
                Currency = "ZAR",
                DonationDate = DateTime.Now,
                PaymentStatus = "Pending"
            };
            db.Donations.Add(pending);
            await db.SaveChangesAsync();
            return pending.DonationId;
        });

        var missing = await TaxCertificateFunctionRunner.RunAsync(_factory, null);
        var unknown = await TaxCertificateFunctionRunner.RunAsync(_factory, 999_999);
        var pending = await TaxCertificateFunctionRunner.RunAsync(_factory, null, $"?donationId={pendingId}");

        Assert.Equal(400, missing.StatusCode);
        Assert.Equal(404, unknown.StatusCode);
        Assert.Equal(400, pending.StatusCode);
        Assert.Contains("must have PaymentStatus 'Completed'", pending.Body.GetProperty("message").GetString());
        Assert.False(await _factory.WithDbAsync(db => db.TaxCertificates.AnyAsync(tc => tc.DonationId == pendingId)));
    }

    [Fact]
    public async Task Donor_CannotRequestACertificateForSomeoneElsesDonation()
    {
        var otherDonationId = await _factory.WithDbAsync(async db =>
        {
            var volunteer = await db.Users.SingleAsync(u => u.Email == DemoUsers.VolunteerEmail);
            var donation = new Donation
            {
                UserId = volunteer.UserId,
                Amount = 90m,
                Currency = "ZAR",
                DonationDate = DateTime.Now,
                PaymentStatus = "Completed"
            };
            db.Donations.Add(donation);
            await db.SaveChangesAsync();
            return donation.DonationId;
        });

        var browser = new BrowserSession(_factory.CreateBrowserClient());
        await browser.SignInAsync(DemoUsers.DonorEmail, DemoUsers.DonorPassword);

        var response = await browser.SubmitFormAsync(
            "/Dashboards/Donor",
            new Dictionary<string, string> { ["donationId"] = otherDonationId.ToString() },
            "/Dashboards/Donor?handler=RequestCertificate");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("That donation was not found on your account.", await browser.GetPageAsync("/Dashboards/Donor"));
        Assert.False(await _factory.WithDbAsync(db => db.TaxCertificates.AnyAsync(tc => tc.DonationId == otherDonationId)));
    }

    [Fact]
    public async Task WrongPassword_ShowsAnError_AndDonorDashboardStaysLocked()
    {
        var browser = new BrowserSession(_factory.CreateBrowserClient());

        var response = await browser.SubmitFormAsync("/Login", new Dictionary<string, string>
        {
            ["Email"] = DemoUsers.DonorEmail,
            ["Password"] = "not-the-password"
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("Invalid email or password.", await browser.GetPageAsync("/Login"));

        var dashboard = await browser.Client.GetAsync("/Dashboards/Donor");
        Assert.Equal(HttpStatusCode.Redirect, dashboard.StatusCode);
        Assert.StartsWith("/Login", BrowserSession.PathOf(dashboard));
    }
}
