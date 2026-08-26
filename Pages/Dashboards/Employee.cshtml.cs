using GiftOfTheGivers.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GiftOfTheGivers.Pages.Dashboards
{
    [Authorize(Roles = "Employee")]
    public class EmployeeModel : PageModel
    {
        private readonly ILogger<EmployeeModel> _logger;
        private readonly ApplicationDbContext _context;

        public EmployeeModel(ILogger<EmployeeModel> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public int ActiveOperations { get; set; }
        public List<ReliefProject> Operations { get; set; } = new();

        public async Task OnGetAsync()
        {
            ActiveOperations = await _context.ReliefProjects
                .CountAsync(p => p.Status == "Active" || p.Status == "Planning");

            Operations = await _context.ReliefProjects
                .OrderByDescending(p => p.CreatedDate)
                .Take(8)
                .ToListAsync();
        }

        public IActionResult OnPost(string operation, string title, string description)
        {
            _logger.LogInformation("Relief update posted for operation: {Operation}", operation);
            return RedirectToPage();
        }
    }
}
