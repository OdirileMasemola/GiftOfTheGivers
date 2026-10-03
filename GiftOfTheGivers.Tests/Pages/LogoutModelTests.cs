using GiftOfTheGivers.Pages;
using GiftOfTheGivers.Tests.TestSupport;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace GiftOfTheGivers.Tests.Pages;

public class LogoutModelTests
{
    private readonly Mock<IAuthenticationService> _auth = new();
    private readonly Mock<ILogger<LogoutModel>> _logger = new();

    private LogoutModel NewLogout()
    {
        var services = new ServiceCollection().AddSingleton(_auth.Object).BuildServiceProvider();
        return new LogoutModel(_logger.Object).WithContext(PageModelSetup.SignedInAs(7, "Donor"), services);
    }

    private void VerifySignedOut() =>
        _auth.Verify(a => a.SignOutAsync(It.IsAny<HttpContext>(), "Cookies", It.IsAny<AuthenticationProperties>()), Times.Once());

    [Fact]
    public async Task Post_SignsOutOfTheCookieSchemeAndRedirectsHome()
    {
        var result = await NewLogout().OnPostAsync();

        var redirect = Assert.IsType<LocalRedirectResult>(result);
        Assert.Equal("/", redirect.Url);
        VerifySignedOut();
        _logger.VerifyLogged(LogLevel.Information, Times.Once());
    }

    [Fact]
    public async Task Get_AlsoSignsOut()
    {
        var result = await NewLogout().OnGetAsync();

        Assert.IsType<LocalRedirectResult>(result);
        VerifySignedOut();
    }
}