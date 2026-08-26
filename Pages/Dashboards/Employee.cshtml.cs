using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GiftOfTheGivers.Pages.Dashboards
{
    [Authorize(Roles = "Employee")]
    public class EmployeeModel : PageModel
    {
        private readonly ILogger<EmployeeModel> _logger;

        public EmployeeModel(ILogger<EmployeeModel> logger)
        {
            _logger = logger;
        }

        public void OnGet()
        {
        }

        public IActionResult OnPost(string operation, string title, string description)
        {
            _logger.LogInformation("Relief update posted for operation: {Operation}", operation);
            return RedirectToPage();
        }
    }
}
