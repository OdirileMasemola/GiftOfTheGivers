using GiftOfTheGivers.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GiftOfTheGivers.Pages.Dashboards
{
    /// <summary>
    /// Employee overview dashboard. Donation cards and recent gifts use DB aggregates;
    /// volunteer and operations widgets are left intact aside from accurate labels.
    /// </summary>
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
        public int PendingVolunteers { get; set; }
        public int TotalVolunteers { get; set; }
        public decimal MonthDonationsZar { get; set; }
        public int TotalOperations { get; set; }
        public int PendingDonations { get; set; }
        public int ActiveSchedules { get; set; }
        public List<ReliefOperation> Operations { get; set; } = new();
        public List<Volunteer> RecentVolunteers { get; set; } = new();
        public List<DonationSummary> RecentDonations { get; set; } = new();

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
                    .Where(d => d.Currency == "ZAR"
                        && d.PaymentStatus == "Completed"
                        && d.DonationDate >= monthStart)
                    .SumAsync(d => (decimal?)d.Amount) ?? 0m;

                // Total operations recorded (not "communities" or "active regions").
                TotalOperations = await _context.ReliefOperations.CountAsync();

                PendingDonations = await _context.Donations
                    .CountAsync(d => d.PaymentStatus == "Pending");

                ActiveSchedules = await _context.DonationSchedules
                    .CountAsync(s => s.Status == "Active");

                Operations = await _context.ReliefOperations
                    .Include(o => o.ReliefRequest)
                    .OrderByDescending(o => o.StartDate)
                    .Take(8)
                    .ToListAsync();

                RecentVolunteers = await _context.Volunteers
                    .Include(v => v.User)
                    .OrderByDescending(v => v.RegistrationDate)
                    .Take(8)
                    .ToListAsync();

                RecentDonations = await _context.Donations.AsNoTracking()
                    .OrderByDescending(d => d.DonationDate)
                    .Take(8)
                    .Select(d => new DonationSummary
                    {
                        DonationDate = d.DonationDate,
                        Amount = d.Amount,
                        Currency = d.Currency,
                        PaymentStatus = d.PaymentStatus,
                        DonorName = d.User == null
                            ? "Unknown"
                            : ((d.User.FirstName ?? "") + " " + (d.User.LastName ?? "")).Trim(),
                        DonorEmail = d.User != null ? d.User.Email : null
                    })
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading employee dashboard data");
                ActiveOperations = 0;
                PendingVolunteers = 0;
                TotalVolunteers = 0;
                MonthDonationsZar = 0;
                TotalOperations = 0;
                PendingDonations = 0;
                ActiveSchedules = 0;
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

        public class DonationSummary
        {
            public DateTime DonationDate { get; set; }
            public decimal Amount { get; set; }
            public string Currency { get; set; } = string.Empty;
            public string PaymentStatus { get; set; } = string.Empty;
            public string DonorName { get; set; } = "Unknown";
            public string? DonorEmail { get; set; }
        }
    }
}
