using System.Text.Json;
using GiftOfTheGivers.Data;
using GiftOfTheGivers.Functions;
using GiftOfTheGivers.Tests.TestSupport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Moq;

namespace GiftOfTheGivers.Tests.Functions;

public class GenerateTaxCertificateTests
{
    private readonly Mock<ILogger<GenerateTaxCertificate>> _logger = new();

    private static HttpRequest Request(string query = "")
    {
        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(query);
        return context.Request;
    }

    private static JsonElement Body(IActionResult result) =>
        JsonSerializer.SerializeToElement(((ObjectResult)result).Value);

    [Fact]
    public async Task Run_CompletedDonation_CreatesCertificate()
    {
        using var db = TestDb.Create();
        var donation = TestDb.AddDonation(db, TestDb.AddUser(db, "donor@example.com"), 1250m);
        var function = new GenerateTaxCertificate(db, _logger.Object);

        var result = await function.Run(Request(), donation.DonationId);

        Assert.IsType<OkObjectResult>(result);
        var body = Body(result);
        Assert.False(body.GetProperty("alreadyExisted").GetBoolean());
        Assert.Matches("^CERT-\\d{8}-[0-9A-F]{8}$", body.GetProperty("certificateNumber").GetString());
        Assert.Equal(1250m, Assert.Single(db.TaxCertificates).CertificateAmount);
    }

    [Fact]
    public async Task Run_SecondCall_ReturnsTheExistingCertificate()
    {
        using var db = TestDb.Create();
        var donation = TestDb.AddDonation(db, TestDb.AddUser(db, "donor@example.com"), 300m);
        var function = new GenerateTaxCertificate(db, _logger.Object);

        var first = Body(await function.Run(Request(), donation.DonationId));
        var second = Body(await function.Run(Request($"?donationId={donation.DonationId}"), null));

        Assert.True(second.GetProperty("alreadyExisted").GetBoolean());
        Assert.Equal(first.GetProperty("certificateNumber").GetString(), second.GetProperty("certificateNumber").GetString());
        Assert.Single(db.TaxCertificates);
    }

    [Theory]
    [InlineData("")]
    [InlineData("?donationId=abc")]
    [InlineData("?donationId=-4")]
    public async Task Run_MissingOrInvalidId_ReturnsBadRequest(string query)
    {
        using var db = TestDb.Create();
        var function = new GenerateTaxCertificate(db, _logger.Object);

        Assert.IsType<BadRequestObjectResult>(await function.Run(Request(query), null));
    }

    [Fact]
    public async Task Run_NonPositiveRouteId_ReturnsBadRequest()
    {
        using var db = TestDb.Create();
        var function = new GenerateTaxCertificate(db, _logger.Object);

        Assert.IsType<BadRequestObjectResult>(await function.Run(Request(), 0));
    }

    [Fact]
    public async Task Run_UnknownDonation_ReturnsNotFound()
    {
        using var db = TestDb.Create();
        var function = new GenerateTaxCertificate(db, _logger.Object);

        Assert.IsType<NotFoundObjectResult>(await function.Run(Request(), 12345));
    }

    [Fact]
    public async Task Run_PendingDonation_IsRejected()
    {
        using var db = TestDb.Create();
        var donation = TestDb.AddDonation(db, TestDb.AddUser(db, "donor@example.com"), 300m, status: "Pending");
        var function = new GenerateTaxCertificate(db, _logger.Object);

        var result = await function.Run(Request(), donation.DonationId);

        Assert.IsType<BadRequestObjectResult>(result);
        Assert.Empty(db.TaxCertificates);
    }

    [Fact]
    public async Task Run_WhenDatabaseFails_Returns500AndLogsError()
    {
        var db = TestDb.Create();
        db.Dispose();
        var function = new GenerateTaxCertificate(db, _logger.Object);

        var result = await function.Run(Request(), 1);

        Assert.Equal(StatusCodes.Status500InternalServerError, ((ObjectResult)result).StatusCode);
        _logger.VerifyLogged(LogLevel.Error, Times.Once());
    }
}
