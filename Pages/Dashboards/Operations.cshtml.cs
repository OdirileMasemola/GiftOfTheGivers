using GiftOfTheGivers.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GiftOfTheGivers.Pages.Dashboards
{
    [Authorize(Roles = "Employee")]
    public class OperationsModel : PageModel
    {
        private static readonly string[] AllowedStatuses = { "Planning", "Active", "Completed" };
        private readonly ApplicationDbContext _context;

        public OperationsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<ReliefProject> Projects { get; set; } = new();
        public int ActiveCount { get; set; }
        public int PlanningCount { get; set; }
        public int CompletedCount { get; set; }

        [BindProperty]
        public string Name { get; set; } = string.Empty;

        [BindProperty]
        public string Location { get; set; } = string.Empty;

        [BindProperty]
        public string Description { get; set; } = string.Empty;

        [BindProperty]
        public string Status { get; set; } = "Planning";

        [BindProperty]
        public DateTime StartDate { get; set; } = DateTime.Today;

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        public async Task<IActionResult> OnPostCreateAsync()
        {
            if (string.IsNullOrWhiteSpace(Name) || string.IsNullOrWhiteSpace(Location))
            {
                ModelState.AddModelError(string.Empty, "Name and location are required.");
                await LoadAsync();
                return Page();
            }

            var status = AllowedStatuses.Contains(Status) ? Status : "Planning";

            _context.ReliefProjects.Add(new ReliefProject
            {
                Name = Name.Trim(),
                Location = Location.Trim(),
                Description = Description?.Trim() ?? string.Empty,
                Status = status,
                StartDate = StartDate == default ? DateTime.Today : StartDate,
                CreatedDate = DateTime.Now,
                EndDate = status == "Completed" ? DateTime.Now : null
            });

            await _context.SaveChangesAsync();
            TempData["OperationsMessage"] = "Relief operation created.";
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUpdateStatusAsync(int id, string status)
        {
            if (!AllowedStatuses.Contains(status))
            {
                TempData["OperationsError"] = "That status is not valid.";
                return RedirectToPage();
            }

            var project = await _context.ReliefProjects.FindAsync(id);
            if (project == null)
            {
                return NotFound();
            }

            project.Status = status;
            project.EndDate = status == "Completed"
                ? project.EndDate ?? DateTime.Now
                : null;

            await _context.SaveChangesAsync();
            TempData["OperationsMessage"] = $"{project.Name} marked as {status}.";
            return RedirectToPage();
        }

        private async Task LoadAsync()
        {
            if (StartDate == default)
            {
                StartDate = DateTime.Today;
            }

            Projects = await _context.ReliefProjects
                .OrderByDescending(p => p.CreatedDate)
                .ToListAsync();

            ActiveCount = Projects.Count(p => p.Status == "Active");
            PlanningCount = Projects.Count(p => p.Status == "Planning");
            CompletedCount = Projects.Count(p => p.Status == "Completed");
        }
    }
}
