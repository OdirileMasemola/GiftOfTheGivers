using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GiftOfTheGivers.Pages
{
    // Replaces the missing Identity Logout page at /Identity/Account/Logout.
    [AllowAnonymous]
    public class LogoutModel : PageModel
    {
        // GET covers a direct visit to /Logout.
        public IActionResult OnGetAsync(string? returnUrl = null)
        {
            return RedirectHome(returnUrl);
        }

        // POST covers the nav/footer/dashboard logout forms.
        public IActionResult OnPostAsync(string? returnUrl = null)
        {
            return RedirectHome(returnUrl);
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
