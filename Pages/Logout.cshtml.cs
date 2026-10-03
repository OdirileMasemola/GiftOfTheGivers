using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GiftOfTheGivers.Pages
{
    // Replaces the missing Identity Logout page at /Identity/Account/Logout.
    [AllowAnonymous]
    public class LogoutModel : PageModel
    {
        private readonly ILogger<LogoutModel> _logger;

        public LogoutModel(ILogger<LogoutModel> logger)
        {
            _logger = logger;
        }

        // GET covers a direct visit to /Logout.
        public async Task<IActionResult> OnGetAsync(string? returnUrl = null)
        {
            await SignOutAsync();
            return RedirectHome(returnUrl);
        }

        // POST covers the nav/footer/dashboard logout forms.
        public async Task<IActionResult> OnPostAsync(string? returnUrl = null)
        {
            await SignOutAsync();
            return RedirectHome(returnUrl);
        }

        // Clears the "Cookies" auth cookie that Login issues.
        private async Task SignOutAsync()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                _logger.LogInformation("User {Name} logged out", User.Identity.Name);
            }

            await HttpContext.SignOutAsync("Cookies");
        }

        // Only follow returnUrl if it stays on this site.
        private IActionResult RedirectHome(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return LocalRedirect("/");
        }
    }
}