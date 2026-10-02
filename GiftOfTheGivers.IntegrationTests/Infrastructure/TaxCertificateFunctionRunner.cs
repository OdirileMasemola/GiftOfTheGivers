using System.Text.Json;
using GiftOfTheGivers.Data;
using GiftOfTheGivers.Functions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GiftOfTheGivers.IntegrationTests.Infrastructure;

/// <summary>
/// Calls the real GenerateTaxCertificate function code in-process against the same
/// test database the web app writes to, instead of the deployed Function App.
/// </summary>
public static class TaxCertificateFunctionRunner
{
    public static async Task<(int StatusCode, JsonElement Body)> RunAsync(
        GiftOfTheGiversWebFactory factory, int? donationId, string query = "")
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<GenerateTaxCertificate>>();

        var context = new DefaultHttpContext();
        context.Request.QueryString = new QueryString(query);

        var result = (ObjectResult)await new GenerateTaxCertificate(db, logger).Run(context.Request, donationId);
        return (result.StatusCode ?? StatusCodes.Status200OK, JsonSerializer.SerializeToElement(result.Value));
    }
}
