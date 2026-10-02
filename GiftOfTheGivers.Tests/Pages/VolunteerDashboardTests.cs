using GiftOfTheGivers.Data;
using GiftOfTheGivers.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Moq;
using VolunteerDashboard = GiftOfTheGivers.Pages.Dashboards.VolunteerModel;

namespace GiftOfTheGivers.Tests.Pages;

public class VolunteerDashboardTests
{
    private static VolunteerDashboard NewDashboard(ApplicationDbContext db, User? user = null) =>
        new VolunteerDashboard(Mock.Of<ILogger<VolunteerDashboard>>(), db)
            .WithContext(user is null ? null : PageModelSetup.SignedInAs(user.UserId, user.Role));

    [Fact]
    public async Task Get_SplitsAssignmentsIntoCurrentAndCompleted()
    {
        using var db = TestDb.Create();
        var user = TestDb.AddUser(db, "naledi@example.com", role: "Volunteer");
        var volunteer = TestDb.AddVolunteer(db, user, status: "Active");
        var active = TestDb.AddOperation(db, "Durban", status: "Active");
        var finished = TestDb.AddOperation(db, "Mthatha", status: "Completed");
        db.VolunteerAssignments.AddRange(
            new VolunteerAssignment { VolunteerId = volunteer.VolunteerId, ReliefOperationId = active.ReliefOperationId, AssignedDate = DateTime.Today },
            new VolunteerAssignment { VolunteerId = volunteer.VolunteerId, ReliefOperationId = finished.ReliefOperationId, AssignedDate = DateTime.Today.AddDays(-30) });
        db.SaveChanges();
        var page = NewDashboard(db, user);

        var result = await page.OnGetAsync();

        Assert.IsType<PageResult>(result);
        Assert.Equal("naledi@example.com", page.CurrentUser!.Email);
        Assert.Equal("Durban", Assert.Single(page.CurrentAssignments).ReliefOperation!.Location);
        Assert.Equal("Mthatha", Assert.Single(page.CompletedAssignments).ReliefOperation!.Location);
        Assert.Equal(2, page.TotalAssignments);
    }

    [Fact]
    public async Task Get_WithoutUserId_RedirectsToLogin()
    {
        using var db = TestDb.Create();

        var result = await NewDashboard(db).OnGetAsync();

        Assert.Equal("/Login", Assert.IsType<RedirectToPageResult>(result).PageName);
    }

    [Fact]
    public async Task Post_ValidAvailability_IsSaved()
    {
        using var db = TestDb.Create();
        var user = TestDb.AddUser(db, "naledi@example.com", role: "Volunteer");
        TestDb.AddVolunteer(db, user);
        var page = NewDashboard(db, user);

        await page.OnPostAsync("Full-time");

        Assert.Equal("Full-time", db.Volunteers.Single().Availability);
        Assert.Equal("Your availability has been updated.", page.TempData["SuccessMessage"]);
    }

    [Fact]
    public async Task Post_MadeUpAvailability_IsRejected()
    {
        using var db = TestDb.Create();
        var user = TestDb.AddUser(db, "naledi@example.com", role: "Volunteer");
        TestDb.AddVolunteer(db, user);
        var page = NewDashboard(db, user);

        await page.OnPostAsync("Every second Tuesday");

        Assert.Equal("Weekends only", db.Volunteers.Single().Availability);
        Assert.Equal("Please choose a valid availability option.", page.TempData["ErrorMessage"]);
    }

    [Fact]
    public async Task Post_UserWithoutVolunteerProfile_IsAskedToRegisterFirst()
    {
        using var db = TestDb.Create();
        var donor = TestDb.AddUser(db, "donor@example.com");
        var page = NewDashboard(db, donor);

        await page.OnPostAsync("Full-time");

        Assert.Equal("You need to register as a volunteer first.", page.TempData["ErrorMessage"]);
    }
}
