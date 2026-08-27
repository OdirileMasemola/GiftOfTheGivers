using GiftOfTheGivers.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GiftOfTheGivers.Pages.Dashboards
{
    public class OperationsModel : PageModel
    {
        private static readonly string[] AllowedStatuses = { "Planning", "Active", "Completed", "Paused" };
        private readonly ApplicationDbContext _context;

        public OperationsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<ReliefOperation> Operations { get; set; } = new();
        public int ActiveCount { get; set; }
        public int PlanningCount { get; set; }
        public int CompletedCount { get; set; }

        [BindProperty]
        public string OperationType { get; set; } = string.Empty;

        [BindProperty]
        public string Location { get; set; } = string.Empty;

        [BindProperty]
        public string Notes { get; set; } = string.Empty;

        [BindProperty]
        public string Status { get; set; } = "Planning";

        [BindProperty]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [BindProperty]
        public int ReliefRequestId { get; set; }

        public async Task OnGetAsync()
        {
            await LoadAsync();
        }

        public async Task<IActionResult> OnPostCreateAsync()
        {
            if (string.IsNullOrWhiteSpace(OperationType) || string.IsNullOrWhiteSpace(Location))
            {
                ModelState.AddModelError(string.Empty, "Operation type and location are required.");
                await LoadAsync();
                return Page();
            }

            var status = AllowedStatuses.Contains(Status) ? Status : "Planning";

            // If no ReliefRequestId provided, create a basic relief operation without a request
            var reliefOperation = new ReliefOperation
            {
                OperationType = OperationType.Trim(),
                Location = Location.Trim(),
                Notes = Notes?.Trim(),
                Status = status,
                StartDate = StartDate == default ? DateTime.Today : StartDate,
                EndDate = status == "Completed" ? DateTime.Now : null,
                ReliefRequestId = ReliefRequestId > 0 ? ReliefRequestId : 1 // Use first request if available, else default
            };

            // Only add if a valid relief request exists or create a default one
            var reliefRequest = await _context.ReliefRequests.FirstOrDefaultAsync();
            if (reliefRequest != null)
            {
                reliefOperation.ReliefRequestId = reliefRequest.ReliefRequestId;
            }
            else
            {
                // Create a default relief request if none exists
                reliefRequest = new ReliefRequest
                {
                    RequestedByUserId = 1, // Default to first user
                    RequestType = "General Relief",
                    Description = "Default relief request",
                    Location = Location.Trim(),
                    RequestDate = DateTime.Now,
                    Priority = "Medium",
                    Status = "Approved"
                };
                _context.ReliefRequests.Add(reliefRequest);
                await _context.SaveChangesAsync();
                reliefOperation.ReliefRequestId = reliefRequest.ReliefRequestId;
            }

            _context.ReliefOperations.Add(reliefOperation);
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

            var operation = await _context.ReliefOperations.FindAsync(id);
            if (operation == null)
            {
                return NotFound();
            }

            operation.Status = status;
            operation.EndDate = status == "Completed"
                ? operation.EndDate ?? DateTime.Now
                : null;

            await _context.SaveChangesAsync();
            TempData["OperationsMessage"] = $"Operation in {operation.Location} marked as {status}.";
            return RedirectToPage();
        }

        private async Task LoadAsync()
        {
            if (StartDate == default)
            {
                StartDate = DateTime.Today;
            }

            Operations = await _context.ReliefOperations
                .Include(o => o.ReliefRequest)
                .OrderByDescending(o => o.StartDate)
                .ToListAsync();

            ActiveCount = Operations.Count(o => o.Status == "Active");
            PlanningCount = Operations.Count(o => o.Status == "Planning");
            CompletedCount = Operations.Count(o => o.Status == "Completed");
        }
    }
}
