using GiftOfTheGivers.Data;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GiftOfTheGivers.Pages.Dashboards
{
    public class DonationsModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public DonationsModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public List<Donation> Donations { get; set; } = new();
        public decimal ThisMonthZar { get; set; }
        public decimal AllTimeZar { get; set; }
        public int RecurringCount { get; set; }

        public async Task OnGetAsync()
        {
            var monthStart = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);

            Donations = await _context.Donations
                .Include(d => d.User)
                .OrderByDescending(d => d.DonationDate)
                .ToListAsync();

            ThisMonthZar = Donations
                .Where(d => d.Currency == "ZAR" && d.DonationDate >= monthStart && d.PaymentStatus == "Completed")
                .Sum(d => d.Amount);

            AllTimeZar = Donations
                .Where(d => d.Currency == "ZAR" && d.PaymentStatus == "Completed")
                .Sum(d => d.Amount);

            // Recurring donations based on DonationSchedules
            RecurringCount = await _context.DonationSchedules
                .CountAsync(ds => ds.Status == "Active");
        }
    }
}
