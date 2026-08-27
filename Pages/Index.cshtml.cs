using GiftOfTheGivers.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GiftOfTheGivers.Pages
{
    public class IndexModel : PageModel
    {
        private readonly ILogger<IndexModel> _logger;
        private readonly ApplicationDbContext _context;

        public int ActiveOperations { get; set; }
        public int RegisteredVolunteers { get; set; }
        public decimal TotalDonations { get; set; }
        public int OperationsSupported { get; set; }
        public List<ReliefOperation> RecentOperations { get; set; } = new();

        public IndexModel(ILogger<IndexModel> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task OnGetAsync()
        {
            try
            {
                // Get count of active relief operations
                ActiveOperations = await _context.ReliefOperations
                    .CountAsync(o => o.Status == "Active" || o.Status == "Planning");

                // Get count of registered volunteers
                RegisteredVolunteers = await _context.Volunteers.CountAsync();

                // Get total donations in ZAR
                TotalDonations = await _context.Donations
                    .Where(d => d.Currency == "ZAR" && d.PaymentStatus == "Completed")
                    .SumAsync(d => d.Amount);

                // Get count of relief operations (communities/operations supported)
                OperationsSupported = await _context.ReliefOperations.CountAsync();

                // Get recent relief operations
                RecentOperations = await _context.ReliefOperations
                    .Include(o => o.ReliefRequest)
                    .OrderByDescending(o => o.StartDate)
                    .Take(6)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading homepage data");
                // Set defaults if there's an error
                ActiveOperations = 0;
                RegisteredVolunteers = 0;
                TotalDonations = 0;
                OperationsSupported = 0;
            }
        }
    }
}
