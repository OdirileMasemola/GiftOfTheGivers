using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using Moq;

namespace GiftOfTheGivers.Tests.TestSupport;

/// <summary>
/// Gives a page model the HttpContext, ModelState and TempData it would normally get from MVC.
/// </summary>
internal static class PageModelSetup
{
    public static T WithContext<T>(this T page, ClaimsPrincipal? user = null, IServiceProvider? services = null)
        where T : PageModel
    {
        var httpContext = new DefaultHttpContext
        {
            User = user ?? new ClaimsPrincipal(new ClaimsIdentity())
        };

        if (services is not null)
        {
            httpContext.RequestServices = services;
        }

        var modelState = new ModelStateDictionary();
        var actionContext = new ActionContext(httpContext, new RouteData(), new PageActionDescriptor(), modelState);

        page.PageContext = new PageContext(actionContext)
        {
            ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), modelState)
        };
        page.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());

        return page;
    }

    public static ClaimsPrincipal SignedInAs(int userId, string role) =>
        new(new ClaimsIdentity(
            new[]
            {
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim(ClaimTypes.Role, role)
            },
            "Cookies"));

    /// <summary>
    /// Checks that a mocked ILogger received a message at the given level.
    /// </summary>
    public static void VerifyLogged<T>(this Mock<ILogger<T>> logger, LogLevel level, Times times) =>
        logger.Verify(
            l => l.Log(
                level,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            times);
}
