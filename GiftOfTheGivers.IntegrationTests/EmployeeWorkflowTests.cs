using System.Net;
using GiftOfTheGivers.Data;
using GiftOfTheGivers.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace GiftOfTheGivers.IntegrationTests;

/// <summary>
/// Employee journeys: run relief operations and manage volunteer applications.
/// </summary>
public class EmployeeWorkflowTests : IClassFixture<GiftOfTheGiversWebFactory>
{
    private readonly GiftOfTheGiversWebFactory _factory;

    public EmployeeWorkflowTests(GiftOfTheGiversWebFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _factory.Output = output;
    }

    private async Task<BrowserSession> SignedInEmployeeAsync()
    {
        var browser = new BrowserSession(_factory.CreateBrowserClient());
        await browser.SignInAsync(DemoUsers.EmployeeEmail, DemoUsers.EmployeePassword);
        return browser;
    }

    [Fact]
    public async Task Employee_CreatesOperation_ThenPostsStatusUpdates()
    {
        var employee = await SignedInEmployeeAsync();

        var created = await employee.SubmitFormAsync(
            "/Dashboards/Operations",
            new Dictionary<string, string>
            {
                ["OperationType"] = "Flood Relief",
                ["Location"] = "Durban North",
                ["Notes"] = "Blankets, water and food parcels",
                ["Status"] = "Active",
                ["StartDate"] = "2026-10-02"
            },
            "/Dashboards/Operations?handler=Create");

        Assert.Equal(HttpStatusCode.Redirect, created.StatusCode);
        var operation = await _factory.WithDbAsync(db => db.ReliefOperations.SingleAsync(o => o.Location == "Durban North"));
        Assert.Equal("Active", operation.Status);
        Assert.Contains("Durban North", await employee.GetPageAsync("/Dashboards/Operations"));

        // Status update: the operation is finished.
        var update = await employee.SubmitFormAsync(
            "/Dashboards/Operations",
            new Dictionary<string, string> { ["id"] = operation.ReliefOperationId.ToString(), ["status"] = "Completed" },
            "/Dashboards/Operations?handler=UpdateStatus");

        Assert.Equal(HttpStatusCode.Redirect, update.StatusCode);
        var updated = await _factory.WithDbAsync(db => db.ReliefOperations.AsNoTracking()
            .SingleAsync(o => o.ReliefOperationId == operation.ReliefOperationId));
        Assert.Equal("Completed", updated.Status);
        Assert.NotNull(updated.EndDate);
    }

    [Fact]
    public async Task Employee_OperationWithoutLocation_IsNotCreated()
    {
        var employee = await SignedInEmployeeAsync();
        var before = await _factory.WithDbAsync(db => db.ReliefOperations.CountAsync());

        var response = await employee.SubmitFormAsync(
            "/Dashboards/Operations",
            new Dictionary<string, string> { ["OperationType"] = "Medical Support", ["Location"] = "" },
            "/Dashboards/Operations?handler=Create");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(before, await _factory.WithDbAsync(db => db.ReliefOperations.CountAsync()));
    }

    [Fact]
    public async Task VolunteerApplies_EmployeeApproves_AndAssignsThemToAnOperation()
    {
        // 1. A member of the public applies through the volunteer form.
        var applicant = new BrowserSession(_factory.CreateBrowserClient());
        var applied = await applicant.SubmitFormAsync("/Volunteer", new List<KeyValuePair<string, string>>
        {
            new("FirstName", "Naledi"),
            new("LastName", "Khumalo"),
            new("Email", "naledi.khumalo@example.com"),
            new("PhoneNumber", "0827654321"),
            new("SelectedSkills", "Medical"),
            new("SelectedSkills", "Logistics"),
            new("Skills", ""),
            new("Availability", "Weekends only")
        });

        Assert.Equal(HttpStatusCode.Redirect, applied.StatusCode);
        Assert.StartsWith("/VolunteerConfirmation", BrowserSession.PathOf(applied));
        Assert.Contains("Naledi", await applicant.GetPageAsync(BrowserSession.PathOf(applied)));

        var volunteer = await _factory.WithDbAsync(db => db.Volunteers.Include(v => v.User)
            .SingleAsync(v => v.User!.Email == "naledi.khumalo@example.com"));
        Assert.Equal("Pending", volunteer.Status);
        Assert.Equal("Medical, Logistics", volunteer.Skills);

        // 2. An employee approves the application.
        var employee = await SignedInEmployeeAsync();
        var approve = await employee.SubmitFormAsync(
            "/Dashboards/Volunteers",
            new Dictionary<string, string>
            {
                ["id"] = volunteer.VolunteerId.ToString(),
                ["status"] = "Approved",
                ["StatusFilter"] = "All"
            },
            "/Dashboards/Volunteers?handler=UpdateStatus");

        Assert.Equal(HttpStatusCode.Redirect, approve.StatusCode);
        Assert.Contains("Naledi Khumalo has been approved.", await employee.GetPageAsync(BrowserSession.PathOf(approve)));

        // 3. The employee assigns the volunteer to an active operation.
        var operationId = await AddOperationAsync("Pietermaritzburg", "Active");
        var detailsUrl = $"/Dashboards/VolunteerDetails/{volunteer.VolunteerId}";
        var assign = await employee.SubmitFormAsync(
            detailsUrl,
            new Dictionary<string, string> { ["ReliefOperationId"] = operationId.ToString() },
            $"{detailsUrl}?handler=Assign");

        Assert.Equal(HttpStatusCode.Redirect, assign.StatusCode);
        Assert.Contains("Volunteer assigned to the relief operation successfully.", await employee.GetPageAsync(detailsUrl));

        // 4. Assigning the same operation again is refused.
        await employee.SubmitFormAsync(
            detailsUrl,
            new Dictionary<string, string> { ["ReliefOperationId"] = operationId.ToString() },
            $"{detailsUrl}?handler=Assign");

        Assert.Contains("already assigned to that relief operation", await employee.GetPageAsync(detailsUrl));
        Assert.Equal(1, await _factory.WithDbAsync(db => db.VolunteerAssignments.CountAsync(a => a.VolunteerId == volunteer.VolunteerId)));
    }

    [Fact]
    public async Task DuplicateVolunteerApplication_IsRejected()
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("FirstName", "Sipho"),
            new("LastName", "Dube"),
            new("Email", "sipho.dube@example.com"),
            new("SelectedSkills", "Construction"),
            new("Availability", "Part-time")
        };

        var first = await new BrowserSession(_factory.CreateBrowserClient()).SubmitFormAsync("/Volunteer", fields);
        var second = await new BrowserSession(_factory.CreateBrowserClient()).SubmitFormAsync("/Volunteer", fields);

        Assert.Equal(HttpStatusCode.Redirect, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Contains("An application already exists for this email address.", await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Employee_CannotAssignAPendingVolunteer()
    {
        var volunteerId = await _factory.WithDbAsync(async db =>
        {
            var user = new User { FirstName = "Pending", LastName = "Person", Email = "pending.person@example.com", PasswordHash = "x", Role = "Volunteer", CreatedAt = DateTime.Now };
            var volunteer = new Volunteer { User = user, Skills = "Driving", Availability = "Occasional", RegistrationDate = DateTime.Now, Status = "Pending" };
            db.Volunteers.Add(volunteer);
            await db.SaveChangesAsync();
            return volunteer.VolunteerId;
        });
        var operationId = await AddOperationAsync("Kimberley", "Active");
        var employee = await SignedInEmployeeAsync();
        var detailsUrl = $"/Dashboards/VolunteerDetails/{volunteerId}";

        await employee.SubmitFormAsync(
            detailsUrl,
            new Dictionary<string, string> { ["ReliefOperationId"] = operationId.ToString() },
            $"{detailsUrl}?handler=Assign");

        Assert.Contains("Only approved or active volunteers can be assigned.", await employee.GetPageAsync(detailsUrl));
        Assert.False(await _factory.WithDbAsync(db => db.VolunteerAssignments.AnyAsync(a => a.VolunteerId == volunteerId)));
    }

    [Fact]
    public async Task EmployeeDashboard_ListsRecentDonationsAndVolunteers()
    {
        var browser = new BrowserSession(_factory.CreateBrowserClient());
        await browser.SubmitFormAsync("/Donate", new Dictionary<string, string> { ["Amount"] = "45", ["Currency"] = "ZAR" });

        var employee = await SignedInEmployeeAsync();
        var dashboard = await employee.GetPageAsync("/Dashboards/Employee");

        Assert.Contains("anonymous@donor.local", dashboard);
    }

    private Task<int> AddOperationAsync(string location, string status) =>
        _factory.WithDbAsync(async db =>
        {
            var request = await db.ReliefRequests.OrderBy(r => r.ReliefRequestId).FirstOrDefaultAsync();
            if (request is null)
            {
                var employee = await db.Users.SingleAsync(u => u.Email == DemoUsers.EmployeeEmail);
                request = new ReliefRequest
                {
                    RequestedByUserId = employee.UserId,
                    RequestType = "Flooding",
                    Description = "Integration test request",
                    Location = location,
                    RequestDate = DateTime.Now,
                    Priority = "High",
                    Status = "Approved"
                };
                db.ReliefRequests.Add(request);
                await db.SaveChangesAsync();
            }

            var operation = new ReliefOperation
            {
                ReliefRequestId = request.ReliefRequestId,
                OperationType = "Search and Rescue",
                Location = location,
                Status = status,
                StartDate = DateTime.Today
            };
            db.ReliefOperations.Add(operation);
            await db.SaveChangesAsync();
            return operation.ReliefOperationId;
        });
}
