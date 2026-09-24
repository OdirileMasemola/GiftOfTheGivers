using System.Security.Claims;
using GiftOfTheGivers.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using GiftOfTheGivers.Helpers;

namespace GiftOfTheGivers.Pages.Dashboards
{
    [Authorize]
    public class DonorModel : PageModel
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DonorModel> _logger;

        public DonorModel(ApplicationDbContext context, ILogger<DonorModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        public User? CurrentUser { get; private set; }
        public IList<Donation> Donations { get; private set; } = new List<Donation>();
        public IList<DonationSchedule> Schedules { get; private set; } = new List<DonationSchedule>();

        public decimal TotalCompletedZar { get; private set; }
        public int CompletedDonationCount { get; private set; }
        public int TaxCertificateCount { get; private set; }
        public int ActiveScheduleCount { get; private set; }
        public Dictionary<string, decimal> TotalsByCurrency { get; private set; } = new(StringComparer.OrdinalIgnoreCase);

        public string? StatusMessage { get; private set; }

        public async Task<IActionResult> OnGetAsync()
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Challenge();
            }

            CurrentUser = await _context.Users.AsNoTracking()
                .FirstOrDefaultAsync(u => u.UserId == userId);

            if (CurrentUser is null)
            {
                return Challenge();
            }

            await LoadDashboardAsync(userId);

            if (TempData["DonorStatusMessage"] is string message)
            {
                StatusMessage = message;
            }

            return Page();
        }

        /// <summary>
        /// Request or view a tax certificate for a donation owned by the current user (IDOR-safe).
        /// Creates a certificate using the same numbering approach as Donate / GenerateTaxCertificate
        /// when the donation is Completed and no certificate exists yet.
        /// </summary>
        public async Task<IActionResult> OnPostRequestCertificateAsync(int donationId)
        {
            if (!TryGetCurrentUserId(out var userId))
            {
                return Challenge();
            }

            var donation = await _context.Donations
                .Include(d => d.TaxCertificates)
                .FirstOrDefaultAsync(d => d.DonationId == donationId);

            if (donation is null || donation.UserId != userId)
            {
                TempData["DonorStatusMessage"] = "That donation was not found on your account.";
                return RedirectToPage();
            }

            if (!string.Equals(donation.PaymentStatus, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                TempData["DonorStatusMessage"] = "Tax certificates are only available for completed donations.";
                return RedirectToPage();
            }

            var existing = donation.TaxCertificates.FirstOrDefault()
                ?? await _context.TaxCertificates.FirstOrDefaultAsync(tc => tc.DonationId == donation.DonationId);

            if (existing is not null)
            {
                TempData["DonorStatusMessage"] = $"Certificate {existing.CertificateNumber} is already available for donation #{donation.DonationId}.";
                return RedirectToPage();
            }

            var certificate = new TaxCertificate
            {
                DonationId = donation.DonationId,
                CertificateNumber = TaxCertificateFormatter.GenerateCertificateNumber(),
                IssueDate = DateTime.Today,
                CertificateAmount = donation.Amount,
                CreatedAt = DateTime.Now
            };

            _context.TaxCertificates.Add(certificate);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Donor {UserId} created tax certificate {CertificateNumber} for donation {DonationId}.",
                userId,
                certificate.CertificateNumber,
                donation.DonationId);

            TempData["DonorStatusMessage"] = $"Certificate {certificate.CertificateNumber} created for donation #{donation.DonationId}.";
            return RedirectToPage();
        }

        private async Task LoadDashboardAsync(int userId)
        {
            Donations = await _context.Donations
                .AsNoTracking()
                .Include(d => d.TaxCertificates)
                .Where(d => d.UserId == userId)
                .OrderByDescending(d => d.DonationDate)
                .ToListAsync();

            Schedules = await _context.DonationSchedules
                .AsNoTracking()
                .Where(s => s.DonorId == userId)
                .OrderByDescending(s => s.CreatedAt)
                .ToListAsync();

            var completed = Donations
                .Where(d => string.Equals(d.PaymentStatus, "Completed", StringComparison.OrdinalIgnoreCase))
                .ToList();

            CompletedDonationCount = completed.Count;
            TaxCertificateCount = completed.Sum(d => d.TaxCertificates.Count);
            ActiveScheduleCount = Schedules.Count(s =>
                string.Equals(s.Status, "Active", StringComparison.OrdinalIgnoreCase));

            TotalsByCurrency = completed
                .GroupBy(d => string.IsNullOrWhiteSpace(d.Currency) ? "ZAR" : d.Currency.ToUpperInvariant())
                .ToDictionary(g => g.Key, g => g.Sum(d => d.Amount), StringComparer.OrdinalIgnoreCase);

            TotalCompletedZar = TotalsByCurrency.TryGetValue("ZAR", out var zarTotal) ? zarTotal : 0m;
        }

        private bool TryGetCurrentUserId(out int userId)
        {
            userId = 0;
            var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(idValue, out userId) && userId > 0;
        }
    }
}
