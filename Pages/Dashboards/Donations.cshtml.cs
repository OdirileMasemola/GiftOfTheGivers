using GiftOfTheGivers.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GiftOfTheGivers.Pages.Dashboards
{
    [Authorize(Roles = "Employee")]
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
                .OrderByDescending(d => d.DonationDate)
                .ToListAsync();

            ThisMonthZar = Donations
                .Where(d => d.Currency == "ZAR" && d.DonationDate >= monthStart)
                .Sum(d => d.Amount);

            AllTimeZar = Donations
                .Where(d => d.Currency == "ZAR")
                .Sum(d => d.Amount);

            RecurringCount = Donations.Count(d => d.DonationType == "Recurring");
        }
    }
}
