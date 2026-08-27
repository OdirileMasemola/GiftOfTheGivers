using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GiftOfTheGivers.Pages.Dashboards
{
    [AllowAnonymous]
    public class DonorModel : PageModel
    {
        private readonly ILogger<DonorModel> _logger;

        public DonorModel(ILogger<DonorModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
        }
    }
}
