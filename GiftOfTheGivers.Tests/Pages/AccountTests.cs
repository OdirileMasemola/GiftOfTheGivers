using System.Security.Claims;
using GiftOfTheGivers.Areas.Identity.Pages.Account;
using GiftOfTheGivers.Data;
using GiftOfTheGivers.Pages;
using GiftOfTheGivers.Tests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace GiftOfTheGivers.Tests.Pages;

public class LoginModelTests
{
    private readonly Mock<IAuthenticationService> _auth = new();
    private readonly Mock<ILogger<LoginModel>> _logger = new();

    private LoginModel NewLogin(ApplicationDbContext db, string email, string password)
    {
        var services = new ServiceCollection().AddSingleton(_auth.Object).BuildServiceProvider();
        return new LoginModel(db, _logger.Object) { Email = email, Password = password }.WithContext(services: services);
    }

    private void VerifySignInCalled(Times times) =>
        _auth.Verify(a => a.SignInAsync(
            It.IsAny<HttpContext>(), "Cookies", It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()), times);

    [Fact]
    public async Task Post_CorrectPassword_SignsInWithRoleClaimAndRedirectsHome()
    {
        using var db = TestDb.Create();
        var employee = TestDb.AddUser(db, "staff@example.com", role: "Employee", password: "Staff@123");
        ClaimsPrincipal? signedIn = null;
        _auth.Setup(a => a.SignInAsync(It.IsAny<HttpContext>(), "Cookies", It.IsAny<ClaimsPrincipal>(), It.IsAny<AuthenticationProperties>()))
            .Callback<HttpContext, string, ClaimsPrincipal, AuthenticationProperties>((_, _, principal, _) => signedIn = principal)
            .Returns(Task.CompletedTask);

        var result = await NewLogin(db, "staff@example.com", "Staff@123").OnPostAsync();

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/", redirect.Url);
        Assert.NotNull(signedIn);
        Assert.True(signedIn!.IsInRole("Employee"));
        Assert.Equal(employee.UserId.ToString(), signedIn.FindFirstValue(ClaimTypes.NameIdentifier));
    }

    [Fact]
    public async Task Post_WrongPassword_DoesNotSignIn()
    {
        using var db = TestDb.Create();
        TestDb.AddUser(db, "staff@example.com", password: "Staff@123");
        var page = NewLogin(db, "staff@example.com", "wrong-password");

        var result = await page.OnPostAsync();

        Assert.IsType<RedirectToPageResult>(result);
        Assert.Equal("Invalid email or password.", page.TempData["LoginError"]);
        VerifySignInCalled(Times.Never());
    }

    [Fact]
    public async Task Post_UnknownEmail_DoesNotSignIn()
    {
        using var db = TestDb.Create();
        var page = NewLogin(db, "nobody@example.com", "whatever");

        await page.OnPostAsync();

        Assert.Equal("Invalid email or password.", page.TempData["LoginError"]);
        VerifySignInCalled(Times.Never());
    }

    [Fact]
    public async Task Post_MissingFields_ReturnsPageWithError()
    {
        using var db = TestDb.Create();
        var page = NewLogin(db, "", "");

        var result = await page.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.False(page.ModelState.IsValid);
        VerifySignInCalled(Times.Never());
    }

    [Fact]
    public async Task DemoDonor_SignsInAndOpensDonorDashboard()
    {
        using var db = TestDb.Create();
        TestDb.AddUser(db, "donor@test.local", password: "Donor@123");

        var result = await NewLogin(db, "", "").OnPostDemoDonorAsync();

        Assert.Equal("/Dashboards/Donor", Assert.IsType<LocalRedirectResult>(result).Url);
        VerifySignInCalled(Times.Once());
    }
}

public class RegisterModelTests
{
    private static RegisterModel NewRegister(ApplicationDbContext db, string email) =>
        new RegisterModel(db, Mock.Of<ILogger<RegisterModel>>())
        {
            Input = new RegisterModel.InputModel
            {
                FirstName = "Sipho",
                LastName = "Ndlovu",
                Email = email,
                Password = "Sipho@123",
                ConfirmPassword = "Sipho@123",
                PhoneNumber = "0831112222"
            }
        }.WithContext();

    [Fact]
    public async Task Post_NewEmail_CreatesDonorWithHashedPassword()
    {
        using var db = TestDb.Create();

        var result = await NewRegister(db, "sipho@example.com").OnPostAsync();

        Assert.Equal("/Login", Assert.IsType<RedirectToPageResult>(result).PageName);
        var user = Assert.Single(db.Users);
        Assert.Equal("Donor", user.Role);
        Assert.NotEqual("Sipho@123", user.PasswordHash);
        Assert.True(SeedData.VerifyPassword("Sipho@123", user.PasswordHash));
    }

    [Fact]
    public async Task Post_EmailAlreadyRegistered_IsRejected()
    {
        using var db = TestDb.Create();
        TestDb.AddUser(db, "sipho@example.com");
        var page = NewRegister(db, "sipho@example.com");

        var result = await page.OnPostAsync();

        Assert.IsType<PageResult>(result);
        Assert.Contains("already registered", page.ModelState[string.Empty]!.Errors[0].ErrorMessage);
        Assert.Single(db.Users);
    }
}
