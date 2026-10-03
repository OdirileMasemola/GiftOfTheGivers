using System.Net;
using GiftOfTheGivers.IntegrationTests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Xunit.Abstractions;

namespace GiftOfTheGivers.IntegrationTests;

/// <summary>
/// Checks that each role only reaches the pages it is meant to.
/// </summary>
public class AccessControlTests : IClassFixture<GiftOfTheGiversWebFactory>
{
    private readonly GiftOfTheGiversWebFactory _factory;

    public AccessControlTests(GiftOfTheGiversWebFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _factory.Output = output;
    }

    public static TheoryData<string> EmployeePages => new()
    {
        "/Dashboards/Employee",
        "/Dashboards/Donations",
        "/Dashboards/Volunteers",
        "/Dashboards/Operations"
    };

    [Theory]
    [InlineData("/")]
    [InlineData("/Donate")]
    [InlineData("/Volunteer")]
    [InlineData("/Login")]
    [InlineData("/About")]
    public async Task PublicPages_OpenWithoutSigningIn(string url)
    {
        var response = await _factory.CreateBrowserClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [MemberData(nameof(EmployeePages))]
    [InlineData("/Dashboards/Donor")]
    public async Task ProtectedPages_SendAnonymousVisitorsToLogin(string url)
    {
        var response = await _factory.CreateBrowserClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/Login?ReturnUrl=", BrowserSession.PathOf(response));
    }

    [Theory]
    [MemberData(nameof(EmployeePages))]
    public async Task EmployeePages_DenyDonors(string url)
    {
        var donor = new BrowserSession(_factory.CreateBrowserClient());
        await donor.SignInAsync(DemoUsers.DonorEmail, DemoUsers.DonorPassword);

        var response = await donor.Client.GetAsync(url);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/AccessDenied", BrowserSession.PathOf(response));
    }

    [Theory]
    [MemberData(nameof(EmployeePages))]
    public async Task EmployeePages_OpenForEmployees(string url)
    {
        var employee = new BrowserSession(_factory.CreateBrowserClient());
        await employee.SignInAsync(DemoUsers.EmployeeEmail, DemoUsers.EmployeePassword);

        var response = await employee.Client.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Donor_CannotChangeAVolunteersStatus()
    {
        var volunteerId = await _factory.WithDbAsync(async db =>
        {
            var user = await db.Users.SingleAsync(u => u.Email == DemoUsers.VolunteerEmail);
            var volunteer = new GiftOfTheGivers.Data.Volunteer
            {
                UserId = user.UserId,
                Skills = "Counselling",
                Availability = "Part-time",
                RegistrationDate = DateTime.Now,
                Status = "Pending"
            };
            db.Volunteers.Add(volunteer);
            await db.SaveChangesAsync();
            return volunteer.VolunteerId;
        });

        var donor = new BrowserSession(_factory.CreateBrowserClient());
        await donor.SignInAsync(DemoUsers.DonorEmail, DemoUsers.DonorPassword);

        // The donor borrows a valid anti-forgery token from a page they can open.
        var response = await donor.SubmitFormAsync(
            "/Donate",
            new Dictionary<string, string> { ["id"] = volunteerId.ToString(), ["status"] = "Approved" },
            "/Dashboards/Volunteers?handler=UpdateStatus");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/AccessDenied", BrowserSession.PathOf(response));
        var status = await _factory.WithDbAsync(db => db.Volunteers.AsNoTracking()
            .Where(v => v.VolunteerId == volunteerId).Select(v => v.Status).SingleAsync());
        Assert.Equal("Pending", status);
    }

    [Fact]
    public async Task AccessDeniedPage_IsShownToSignedInDonor()
    {
        var donor = new BrowserSession(_factory.CreateBrowserClient());
        await donor.SignInAsync(DemoUsers.DonorEmail, DemoUsers.DonorPassword);

        var response = await donor.Client.GetAsync("/AccessDenied");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsTheSession_SoTheDashboardNeedsLoginAgain()
    {
        var donor = new BrowserSession(_factory.CreateBrowserClient());
        await donor.SignInAsync(DemoUsers.DonorEmail, DemoUsers.DonorPassword);
        Assert.Equal(HttpStatusCode.OK, (await donor.Client.GetAsync("/Dashboards/Donor")).StatusCode);

        // Same form the navigation bar posts.
        var logout = await donor.SubmitFormAsync("/Donate", new Dictionary<string, string>(), "/Logout?returnUrl=%2F");

        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal("/", BrowserSession.PathOf(logout));
        var afterLogout = await donor.Client.GetAsync("/Dashboards/Donor");
        Assert.Equal(HttpStatusCode.Redirect, afterLogout.StatusCode);
        Assert.StartsWith("/Login?ReturnUrl=", BrowserSession.PathOf(afterLogout));
    }

    [Fact]
    public async Task Logout_IgnoresReturnUrlsToOtherSites()
    {
        var employee = new BrowserSession(_factory.CreateBrowserClient());
        await employee.SignInAsync(DemoUsers.EmployeeEmail, DemoUsers.EmployeePassword);

        var logout = await employee.Client.GetAsync("/Logout?returnUrl=https%3A%2F%2Fevil.example.com");

        Assert.Equal(HttpStatusCode.Redirect, logout.StatusCode);
        Assert.Equal("/", BrowserSession.PathOf(logout));
        Assert.Equal(HttpStatusCode.Redirect, (await employee.Client.GetAsync("/Dashboards/Employee")).StatusCode);
    }
}