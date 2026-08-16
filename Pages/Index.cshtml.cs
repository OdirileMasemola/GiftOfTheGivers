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

        public int ActiveProjects { get; set; }
        public int RegisteredVolunteers { get; set; }
        public decimal TotalDonations { get; set; }
        public int CommunitiesSupported { get; set; }
        public List<ReliefProject> RecentProjects { get; set; } = new();

        public IndexModel(ILogger<IndexModel> logger, ApplicationDbContext context)
        {
            _logger = logger;
            _context = context;
        }

        public async Task OnGetAsync()
        {
            // Get statistics
            ActiveProjects = await _context.ReliefProjects
                .CountAsync(p => p.Status == "Active" || p.Status == "Planning");

            RegisteredVolunteers = await _context.Volunteers.CountAsync();

            TotalDonations = await _context.Donations
                .Where(d => d.Currency == "ZAR")
                .SumAsync(d => d.Amount);

            CommunitiesSupported = await _context.ReliefProjects.CountAsync();

            // Get recent projects
            RecentProjects = await _context.ReliefProjects
                .OrderByDescending(p => p.CreatedDate)
                .Take(6)
                .ToListAsync();
        }
    }
}
