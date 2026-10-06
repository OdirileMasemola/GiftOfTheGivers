using System.Security.Claims;
using GiftOfTheGivers.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GiftOfTheGivers.Pages.Dashboards
{
    /// <summary>
    /// Printable tax certificate for one of the signed-in donor's own donations.
    /// Donors print it or use the browser's "Save as PDF" option to download a copy.
    /// </summary>
    [Authorize]
    public class TaxCertificateModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public TaxCertificateModel(ApplicationDbContext context)
        {
            _context = context;
        }

        public Donation? Donation { get; private set; }
        public TaxCertificate? Certificate { get; private set; }
        public User? Donor { get; private set; }

        public async Task<IActionResult> OnGetAsync(int donationId)
        {
            var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!int.TryParse(idValue, out var userId) || userId <= 0)
            {
                return Challenge();
            }

            Donation = await _context.Donations
                .AsNoTracking()
                .Include(d => d.TaxCertificates)
                .Include(d => d.User)
                .FirstOrDefaultAsync(d => d.DonationId == donationId);

            // Same "not found" answer for a missing donation and someone else's donation (IDOR-safe).
            if (Donation is null || Donation.UserId != userId)
            {
                return NotFound();
            }

            Certificate = Donation.TaxCertificates.FirstOrDefault();
            if (Certificate is null)
            {
                TempData["DonorStatusMessage"] = "Request a certificate for this donation first.";
                return RedirectToPage("/Dashboards/Donor");
            }

            Donor = Donation.User;
            return Page();
        }
    }
}
