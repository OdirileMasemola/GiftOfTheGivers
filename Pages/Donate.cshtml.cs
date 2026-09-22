using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using GiftOfTheGivers.Data;
using GiftOfTheGivers.Validation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace GiftOfTheGivers.Pages
{
    public class DonateModel : PageModel
    {

private readonly ApplicationDbContext _context;
        private readonly ILogger<DonateModel> _logger;

        [BindProperty]
        [Required(ErrorMessage = "Please enter a donation amount.")]
        [Range(0.01, 1_000_000, ErrorMessage = "Amount must be greater than 0 and at most 1,000,000.")]
        public decimal? Amount { get; set; }

        [BindProperty]
        [Required(ErrorMessage = "Please select a currency.")]
        public string Currency { get; set; } = "ZAR";

        public DonateModel(ApplicationDbContext context, ILogger<DonateModel> logger)
        {
            _context = context;
            _logger = logger;
        }

        public void OnGet()
        {
        }

        public async Task<IActionResult> OnPostAsync()
        {
            ValidateDonationInput();

            if (!ModelState.IsValid)
            {
                return Page();
            }

            try
            {
                var donorUser = await ResolveDonorUserAsync();
                if (donorUser is null)
                {
                    ModelState.AddModelError(string.Empty, "Unable to attribute this donation. Please try again or sign in.");
                    return Page();
                }

                var amount = Amount!.Value;
                var currency = Currency.Trim().ToUpperInvariant();

                // Simulated payment model: record as Completed. UI copy must stay honest.
                var donation = new Donation
                {
                    UserId = donorUser.UserId,
                    Amount = amount,
                    Currency = currency,
                    DonationDate = DateTime.Now,
                    PaymentStatus = "Completed",
                    PaymentReference = $"PAY-{DateTime.Now:yyyyMMddHHmmss}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}"
                };

                _context.Donations.Add(donation);
                await _context.SaveChangesAsync();

                await EnsureTaxCertificateAsync(donation);

                TempData["DonationMessage"] =
                    $"Donation recorded: {amount:N2} {currency}. Reference {donation.PaymentReference}. No real payment was processed.";
                TempData["DonationSuccess"] = true;

                // PRG: redirect after successful POST to avoid duplicate submits on refresh.
                return RedirectToPage("/Donate");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing donation");
                ModelState.AddModelError(string.Empty, "We could not record your donation right now. Please try again.");
                return Page();
            }
        }

        private void ValidateDonationInput()
        {
            if (!DonationRules.IsValidAmount(Amount, out var amountError))
            {
                ModelState.AddModelError(nameof(Amount), amountError!);
            }

            if (!DonationRules.IsValidCurrency(Currency, out var currencyError))
            {
                ModelState.AddModelError(nameof(Currency), currencyError!);
            }
        }

        /// <summary>
        /// Prefer the signed-in user (ClaimTypes.NameIdentifier → Users.UserId).
        /// Anonymous guests keep an intentional public donate path via a single
        /// shared guest account (anonymous@donor.local). Signed-in donors are never
        /// forced onto donor@test.local.
        /// </summary>
        private async Task<User?> ResolveDonorUserAsync()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
                if (int.TryParse(idValue, out var userId) && userId > 0)
                {
                    var authenticated = await _context.Users.FirstOrDefaultAsync(u => u.UserId == userId);
                    if (authenticated is not null)
                    {
                        return authenticated;
                    }

                    _logger.LogWarning(
                        "Authenticated donation with NameIdentifier '{Id}' did not match a Users.UserId row.",
                        idValue);
                }
            }

            const string guestEmail = "anonymous@donor.local";
            var guest = await _context.Users.FirstOrDefaultAsync(u => u.Email == guestEmail);
            if (guest is not null)
            {
                return guest;
            }

            guest = new User
            {
                FirstName = "Anonymous",
                LastName = "Donor",
                Email = guestEmail,
                PasswordHash = string.Empty,
                Role = "Donor",
                CreatedAt = DateTime.Now
            };
            _context.Users.Add(guest);
            await _context.SaveChangesAsync();
            return guest;
        }

        /// <summary>
        /// One certificate per donation (same uniqueness rule as GenerateTaxCertificate Function).
        /// Only for Completed donations; skip if a cert already exists.
        /// </summary>
        private async Task EnsureTaxCertificateAsync(Donation donation)
        {
            if (!DonationRules.CanIssueTaxCertificate(donation.PaymentStatus))
            {
                return;
            }

            var exists = await _context.TaxCertificates
                .AnyAsync(tc => tc.DonationId == donation.DonationId);
            if (exists)
            {
                return;
            }

            var taxCertificate = new TaxCertificate
            {
                DonationId = donation.DonationId,
                CertificateNumber = $"CERT-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}",
                IssueDate = DateTime.Today,
                CertificateAmount = donation.Amount,
                CreatedAt = DateTime.Now
            };

            _context.TaxCertificates.Add(taxCertificate);
            await _context.SaveChangesAsync();
        }
    }
}
