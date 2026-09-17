using GiftOfTheGivers.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace GiftOfTheGivers.Functions;

public class GenerateTaxCertificate
{
    private readonly ApplicationDbContext _context;
    private readonly ILogger<GenerateTaxCertificate> _logger;

    public GenerateTaxCertificate(ApplicationDbContext context, ILogger<GenerateTaxCertificate> logger)
    {
        _context = context;
        _logger = logger;
    }

    [Function("GenerateTaxCertificate")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "get", "post", Route = "GenerateTaxCertificate/{donationId:int?}")]
        HttpRequest req,
        int? donationId)
    {
        try
        {
            // Allow donationId from route or query string for Postman flexibility
            if (!donationId.HasValue)
            {
                var queryValue = req.Query["donationId"].FirstOrDefault();
                if (!int.TryParse(queryValue, out var parsedId) || parsedId <= 0)
                {
                    _logger.LogWarning("GenerateTaxCertificate called with missing or invalid donationId.");
                    return new BadRequestObjectResult(new
                    {
                        success = false,
                        message = "A valid donationId is required. Provide it in the route or as a donationId query parameter."
                    });
                }

                donationId = parsedId;
            }

            if (donationId.Value <= 0)
            {
                _logger.LogWarning("GenerateTaxCertificate called with non-positive donationId {DonationId}.", donationId);
                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = "donationId must be a positive integer."
                });
            }

            _logger.LogInformation("Generating tax certificate for donation {DonationId}.", donationId.Value);

            var donation = await _context.Donations
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.DonationId == donationId.Value);

            if (donation is null)
            {
                _logger.LogWarning("Donation {DonationId} was not found.", donationId.Value);
                return new NotFoundObjectResult(new
                {
                    success = false,
                    message = $"Donation {donationId.Value} was not found."
                });
            }

            if (!string.Equals(donation.PaymentStatus, "Completed", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning(
                    "Donation {DonationId} has payment status '{PaymentStatus}' and cannot receive a tax certificate.",
                    donation.DonationId,
                    donation.PaymentStatus);

                return new BadRequestObjectResult(new
                {
                    success = false,
                    message = $"Donation {donation.DonationId} must have PaymentStatus 'Completed' before a tax certificate can be generated. Current status: '{donation.PaymentStatus}'."
                });
            }

            var existingCertificate = await _context.TaxCertificates
                .AsNoTracking()
                .FirstOrDefaultAsync(tc => tc.DonationId == donation.DonationId);

            if (existingCertificate is not null)
            {
                _logger.LogInformation(
                    "Tax certificate already exists for donation {DonationId}: {CertificateNumber}.",
                    donation.DonationId,
                    existingCertificate.CertificateNumber);

                return new OkObjectResult(new
                {
                    success = true,
                    alreadyExisted = true,
                    message = "A tax certificate already exists for this donation.",
                    taxCertificateId = existingCertificate.TaxCertificateId,
                    donationId = existingCertificate.DonationId,
                    certificateNumber = existingCertificate.CertificateNumber,
                    issueDate = existingCertificate.IssueDate,
                    certificateAmount = existingCertificate.CertificateAmount,
                    createdAt = existingCertificate.CreatedAt
                });
            }

            // Match certificate numbering used by Pages/Donate.cshtml.cs
            var certificate = new TaxCertificate
            {
                DonationId = donation.DonationId,
                CertificateNumber = $"CERT-{DateTime.Now:yyyyMMdd}-{Guid.NewGuid().ToString()[..8].ToUpperInvariant()}",
                IssueDate = DateTime.Today,
                CertificateAmount = donation.Amount,
                CreatedAt = DateTime.Now
            };

            _context.TaxCertificates.Add(certificate);
            await _context.SaveChangesAsync();

            _logger.LogInformation(
                "Created tax certificate {CertificateNumber} for donation {DonationId}.",
                certificate.CertificateNumber,
                donation.DonationId);

            return new OkObjectResult(new
            {
                success = true,
                alreadyExisted = false,
                message = "Tax certificate generated successfully.",
                taxCertificateId = certificate.TaxCertificateId,
                donationId = certificate.DonationId,
                certificateNumber = certificate.CertificateNumber,
                issueDate = certificate.IssueDate,
                certificateAmount = certificate.CertificateAmount,
                createdAt = certificate.CreatedAt
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error while generating a tax certificate.");
            return new ObjectResult(new
            {
                success = false,
                message = "An unexpected error occurred while generating the tax certificate."
            })
            {
                StatusCode = StatusCodes.Status500InternalServerError
            };
        }
    }
}
