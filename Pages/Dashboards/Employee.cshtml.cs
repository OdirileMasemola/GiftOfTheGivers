using GiftOfTheGivers.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GiftOfTheGivers.Pages.Dashboards
{
    [AllowAnonymous]
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
        public int PendingVolunteers { get; set; }
        public int TotalVolunteers { get; set; }
        public decimal MonthDonationsZar { get; set; }
        public int CommunitiesSupported { get; set; }
        public List<ReliefOperation> Operations { get; set; } = new();
        public List<Volunteer> RecentVolunteers { get; set; } = new();
        public List<Donation> RecentDonations { get; set; } = new();

        public async Task OnGetAsync()
        {
            try
            {
                var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

                ActiveOperations = await _context.ReliefOperations
                    .CountAsync(o => o.Status == "Active" || o.Status == "Planning");

                PendingVolunteers = await _context.Volunteers
                    .CountAsync(v => v.Status == "Pending");

                TotalVolunteers = await _context.Volunteers.CountAsync();

                MonthDonationsZar = await _context.Donations
                    .Where(d => d.Currency == "ZAR" && d.DonationDate >= monthStart)
                    .SumAsync(d => (decimal?)d.Amount) ?? 0;

                CommunitiesSupported = await _context.ReliefOperations.CountAsync();

                Operations = await _context.ReliefOperations
                    .Include(o => o.ReliefRequest)
                    .OrderByDescending(o => o.StartDate)
                    .Take(8)
                    .ToListAsync();

                RecentVolunteers = await _context.Volunteers
                    .OrderByDescending(v => v.RegistrationDate)
                    .Take(8)
                    .ToListAsync();

                RecentDonations = await _context.Donations
                    .OrderByDescending(d => d.DonationDate)
                    .Take(8)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading employee dashboard data");
                // Initialize with empty/zero values on error
                ActiveOperations = 0;
                PendingVolunteers = 0;
                TotalVolunteers = 0;
                MonthDonationsZar = 0;
                CommunitiesSupported = 0;
                Operations = new();
                RecentVolunteers = new();
                RecentDonations = new();
            }
        }

        public IActionResult OnPost(string operation, string title, string description)
        {
            _logger.LogInformation("Relief update posted for operation: {Operation}", operation);
            return RedirectToPage();
        }
    }
}
