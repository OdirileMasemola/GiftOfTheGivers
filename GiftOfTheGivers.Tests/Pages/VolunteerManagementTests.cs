using GiftOfTheGivers.Pages.Dashboards;
using GiftOfTheGivers.Tests.TestSupport;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GiftOfTheGivers.Tests.Pages;

public class VolunteerManagementTests
{
    [Fact]
    public async Task UpdateStatus_ApprovesVolunteerAndShowsTheirName()
    {
        using var db = TestDb.Create();
        var user = TestDb.AddUser(db, "naledi@example.com", role: "Volunteer");
        var volunteer = TestDb.AddVolunteer(db, user);
        var page = new VolunteersModel(db).WithContext();

        await page.OnPostUpdateStatusAsync(volunteer.VolunteerId, "Approved");

        Assert.Equal("Approved", db.Volunteers.Single().Status);
        Assert.Equal("naledi Tester has been approved.", page.TempData["VolunteersMessage"]);
    }

    [Fact]
    public async Task UpdateStatus_UnknownStatus_LeavesVolunteerUnchanged()
    {
        using var db = TestDb.Create();
        var volunteer = TestDb.AddVolunteer(db, TestDb.AddUser(db, "naledi@example.com"));
        var page = new VolunteersModel(db).WithContext();

        await page.OnPostUpdateStatusAsync(volunteer.VolunteerId, "Deleted");

        Assert.Equal("Pending", db.Volunteers.Single().Status);
        Assert.Equal("That status is not valid.", page.TempData["VolunteersError"]);
    }

    [Fact]
    public async Task UpdateStatus_MissingVolunteer_ReturnsNotFound()
    {
        using var db = TestDb.Create();
        var page = new VolunteersModel(db).WithContext();

        Assert.IsType<NotFoundResult>(await page.OnPostUpdateStatusAsync(404, "Approved"));
    }

    [Fact]
    public async Task Get_FiltersBySkillSearchAndStatus()
    {
        using var db = TestDb.Create();
        TestDb.AddVolunteer(db, TestDb.AddUser(db, "a@example.com"), status: "Approved", skills: "Medical");
        TestDb.AddVolunteer(db, TestDb.AddUser(db, "b@example.com"), status: "Approved", skills: "Logistics");
        TestDb.AddVolunteer(db, TestDb.AddUser(db, "c@example.com"), status: "Pending", skills: "Logistics");

        var page = new VolunteersModel(db) { Search = "Logistics", StatusFilter = "Approved" };
        await page.OnGetAsync();

        var match = Assert.Single(page.Volunteers);
        Assert.Equal("b@example.com", match.User!.Email);
        Assert.Equal(1, page.PendingCount);
        Assert.Equal(2, page.ApprovedCount);
    }

    [Fact]
    public async Task Assign_ApprovedVolunteerToActiveOperation_CreatesAssignment()
    {
        using var db = TestDb.Create();
        var volunteer = TestDb.AddVolunteer(db, TestDb.AddUser(db, "a@example.com"), status: "Approved");
        var operation = TestDb.AddOperation(db, "Durban");
        var page = new VolunteerDetailsModel(db) { ReliefOperationId = operation.ReliefOperationId }.WithContext();

        await page.OnPostAssignAsync(volunteer.VolunteerId);

        var assignment = Assert.Single(db.VolunteerAssignments);
        Assert.Equal(operation.ReliefOperationId, assignment.ReliefOperationId);
    }

    [Fact]
    public async Task Assign_PendingVolunteer_IsRefused()
    {
        using var db = TestDb.Create();
        var volunteer = TestDb.AddVolunteer(db, TestDb.AddUser(db, "a@example.com"), status: "Pending");
        var operation = TestDb.AddOperation(db, "Durban");
        var page = new VolunteerDetailsModel(db) { ReliefOperationId = operation.ReliefOperationId }.WithContext();

        await page.OnPostAssignAsync(volunteer.VolunteerId);

        Assert.Empty(db.VolunteerAssignments);
        Assert.Equal("Only approved or active volunteers can be assigned.", page.TempData["VolunteersError"]);
    }

    [Fact]
    public async Task Assign_SameOperationTwice_IsRefused()
    {
        using var db = TestDb.Create();
        var volunteer = TestDb.AddVolunteer(db, TestDb.AddUser(db, "a@example.com"), status: "Active");
        var operation = TestDb.AddOperation(db, "Durban");

        await new VolunteerDetailsModel(db) { ReliefOperationId = operation.ReliefOperationId }
            .WithContext().OnPostAssignAsync(volunteer.VolunteerId);
        var second = new VolunteerDetailsModel(db) { ReliefOperationId = operation.ReliefOperationId }.WithContext();
        await second.OnPostAssignAsync(volunteer.VolunteerId);

        Assert.Single(db.VolunteerAssignments);
        Assert.Contains("already assigned", (string)second.TempData["VolunteersError"]!);
    }

    [Fact]
    public async Task Assign_CompletedOperation_IsRefused()
    {
        using var db = TestDb.Create();
        var volunteer = TestDb.AddVolunteer(db, TestDb.AddUser(db, "a@example.com"), status: "Approved");
        var operation = TestDb.AddOperation(db, "Durban", status: "Completed");
        var page = new VolunteerDetailsModel(db) { ReliefOperationId = operation.ReliefOperationId }.WithContext();

        await page.OnPostAssignAsync(volunteer.VolunteerId);

        Assert.Empty(db.VolunteerAssignments);
        Assert.Equal("Please select an eligible relief operation.", page.TempData["VolunteersError"]);
    }

    [Fact]
    public async Task Details_ListsOnlyOperationsThatCanTakeVolunteers()
    {
        using var db = TestDb.Create();
        var volunteer = TestDb.AddVolunteer(db, TestDb.AddUser(db, "a@example.com"), status: "Approved");
        TestDb.AddOperation(db, "Durban", status: "Active");
        TestDb.AddOperation(db, "Gqeberha", status: "Planning");
        TestDb.AddOperation(db, "Mthatha", status: "Completed");
        var page = new VolunteerDetailsModel(db).WithContext();

        var result = await page.OnGetAsync(volunteer.VolunteerId);

        Assert.IsType<PageResult>(result);
        Assert.Equal(2, page.AvailableOperations.Count);
        Assert.IsType<NotFoundResult>(await new VolunteerDetailsModel(db).WithContext().OnGetAsync(999));
    }
}

public class OperationsModelTests
{
    [Fact]
    public async Task Create_WithoutLocation_ReturnsPageWithError()
    {
        using var db = TestDb.Create();
        var page = new OperationsModel(db) { OperationType = "Flood Relief", Location = " " }.WithContext();

        var result = await page.OnPostCreateAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(page.ModelState.IsValid);
        Assert.Empty(db.ReliefOperations);
    }

    [Fact]
    public async Task Create_UnknownStatus_FallsBackToPlanning()
    {
        using var db = TestDb.Create();
        var page = new OperationsModel(db)
        {
            OperationType = " Flood Relief ",
            Location = " Durban ",
            Notes = "Blankets and water",
            Status = "Whatever"
        }.WithContext();

        await page.OnPostCreateAsync();

        var operation = Assert.Single(db.ReliefOperations);
        Assert.Equal("Planning", operation.Status);
        Assert.Equal("Durban", operation.Location);
        Assert.Single(db.ReliefRequests);
    }

    [Fact]
    public async Task UpdateStatus_Completed_SetsEndDate_AndActiveClearsIt()
    {
        using var db = TestDb.Create();
        var operation = TestDb.AddOperation(db, "Durban");

        await new OperationsModel(db).WithContext().OnPostUpdateStatusAsync(operation.ReliefOperationId, "Completed");
        Assert.NotNull(db.ReliefOperations.Single().EndDate);

        await new OperationsModel(db).WithContext().OnPostUpdateStatusAsync(operation.ReliefOperationId, "Active");
        Assert.Null(db.ReliefOperations.Single().EndDate);
    }

    [Fact]
    public async Task UpdateStatus_InvalidStatusOrMissingOperation_IsHandled()
    {
        using var db = TestDb.Create();
        var operation = TestDb.AddOperation(db, "Durban");
        var page = new OperationsModel(db).WithContext();

        await page.OnPostUpdateStatusAsync(operation.ReliefOperationId, "Cancelled");

        Assert.Equal("Active", db.ReliefOperations.Single().Status);
        Assert.Equal("That status is not valid.", page.TempData["OperationsError"]);
        Assert.IsType<NotFoundResult>(await new OperationsModel(db).WithContext().OnPostUpdateStatusAsync(999, "Paused"));
    }
}
