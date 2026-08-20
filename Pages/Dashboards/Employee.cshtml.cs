using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace GiftOfTheGivers.Pages.Dashboards
{
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
            // For Part 1 prototype, simply log and redirect
            _logger.LogInformation($"Relief update posted for operation: {operation}");

            // In production, this would save to database
            // For now, just redirect back to dashboard
            return RedirectToPage();
        }
    }
}
