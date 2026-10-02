using GiftOfTheGivers.Data;
using GiftOfTheGivers.Pages.Dashboards;
using GiftOfTheGivers.Tests.TestSupport;
using Microsoft.Extensions.Logging;
using Moq;

namespace GiftOfTheGivers.Tests.Pages;

public class EmployeeReportingTests
{
    [Fact]
    public async Task EmployeeDashboard_MonthTotalCountsOnlyCompletedZarDonationsThisMonth()
    {
        using var db = TestDb.Create();
        var donor = TestDb.AddUser(db, "donor@example.com");
        var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

        TestDb.AddDonation(db, donor, 1000m, date: monthStart.AddHours(9));
        TestDb.AddDonation(db, donor, 500m);
        TestDb.AddDonation(db, donor, 700m, date: monthStart.AddDays(-1));
        TestDb.AddDonation(db, donor, 300m, status: "Pending");
        TestDb.AddDonation(db, donor, 80m, currency: "USD");

        TestDb.AddVolunteer(db, donor, status: "Pending");
        TestDb.AddOperation(db, "Durban", status: "Active");
        TestDb.AddOperation(db, "Gqeberha", status: "Planning");
        TestDb.AddOperation(db, "Mthatha", status: "Completed");

        var page = new EmployeeModel(Mock.Of<ILogger<EmployeeModel>>(), db);

        await page.OnGetAsync();

        Assert.Equal(1500m, page.MonthDonationsZar);
        Assert.Equal(1, page.PendingDonations);
        Assert.Equal(1, page.PendingVolunteers);
        Assert.Equal(2, page.ActiveOperations);
        Assert.Equal(3, page.TotalOperations);
        Assert.Equal(5, page.RecentDonations.Count);
    }

    [Fact]
    public async Task EmployeeDashboard_WhenDatabaseIsUnavailable_ShowsZerosAndLogsError()
    {
        var logger = new Mock<ILogger<EmployeeModel>>();
        var db = TestDb.Create();
        db.Dispose();

        var page = new EmployeeModel(logger.Object, db);
        await page.OnGetAsync();

        Assert.Equal(0m, page.MonthDonationsZar);
        Assert.Empty(page.RecentDonations);
        logger.VerifyLogged(LogLevel.Error, Times.Once());
    }

    [Fact]
    public async Task DonationsReport_FiltersByStatusAndSearchAndSumsAllTimeZar()
    {
        using var db = TestDb.Create();
        var thandi = TestDb.AddUser(db, "thandi@example.com");
        var sipho = TestDb.AddUser(db, "sipho@example.com");

        TestDb.AddDonation(db, thandi, 100m);
        TestDb.AddDonation(db, thandi, 50m, status: "Failed");
        TestDb.AddDonation(db, sipho, 200m);
        TestDb.AddDonation(db, sipho, 75m, status: "Pending");

        var page = new DonationsModel(db) { Status = "completed", Search = "thandi" };
        await page.OnGetAsync();

        var row = Assert.Single(page.Donations);
        Assert.Equal(100m, row.Amount);
        Assert.Equal("thandi@example.com", row.DonorEmail);
        Assert.Equal(300m, page.AllTimeZar);
        Assert.Equal(2, page.CompletedCount);
        Assert.Equal(1, page.PendingCount);
        Assert.Equal(1, page.FailedCount);
    }

    [Fact]
    public async Task DonationsReport_PagesResultsFifteenAtATime()
    {
        using var db = TestDb.Create();
        var donor = TestDb.AddUser(db, "donor@example.com");
        for (var i = 1; i <= 20; i++)
        {
            TestDb.AddDonation(db, donor, i, date: DateTime.Today.AddDays(-i));
        }

        var page = new DonationsModel(db) { PageIndex = 2 };
        await page.OnGetAsync();

        Assert.Equal(20, page.FilteredTotal);
        Assert.Equal(2, page.TotalPages);
        Assert.Equal(5, page.Donations.Count);
    }

    [Fact]
    public async Task DonationsReport_DateRangeIncludesTheWholeEndDay()
    {
        using var db = TestDb.Create();
        var donor = TestDb.AddUser(db, "donor@example.com");
        TestDb.AddDonation(db, donor, 10m, date: new DateTime(2026, 9, 1, 8, 0, 0));
        TestDb.AddDonation(db, donor, 20m, date: new DateTime(2026, 9, 30, 23, 30, 0));
        TestDb.AddDonation(db, donor, 30m, date: new DateTime(2026, 10, 1, 0, 30, 0));

        var page = new DonationsModel(db) { From = new DateTime(2026, 9, 1), To = new DateTime(2026, 9, 30) };
        await page.OnGetAsync();

        Assert.Equal(2, page.FilteredTotal);
        Assert.DoesNotContain(page.Donations, d => d.Amount == 30m);
    }
}
