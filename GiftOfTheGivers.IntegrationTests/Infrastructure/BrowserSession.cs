using System.Net;
using System.Text.RegularExpressions;

namespace GiftOfTheGivers.IntegrationTests.Infrastructure;

/// <summary>
/// Acts like a user in a browser: opens a page, reads its anti-forgery token,
/// and submits the form with the same cookies.
/// </summary>
public sealed class BrowserSession
{
    private static readonly Regex AntiForgeryToken =
        new("name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"", RegexOptions.Compiled);

    public BrowserSession(HttpClient client) => Client = client;

    public HttpClient Client { get; }

    public async Task<string> GetPageAsync(string url)
    {
        var response = await Client.GetAsync(url);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    public async Task<HttpResponseMessage> SubmitFormAsync(
        string pageUrl,
        IEnumerable<KeyValuePair<string, string>> fields,
        string? postUrl = null)
    {
        var html = await GetPageAsync(pageUrl);
        var match = AntiForgeryToken.Match(html);
        Assert.True(match.Success, $"No anti-forgery token found on {pageUrl}");

        var form = fields.Append(new("__RequestVerificationToken", WebUtility.HtmlDecode(match.Groups[1].Value)));
        return await Client.PostAsync(postUrl ?? pageUrl, new FormUrlEncodedContent(form));
    }

    public Task<HttpResponseMessage> SubmitFormAsync(string pageUrl, Dictionary<string, string> fields, string? postUrl = null) =>
        SubmitFormAsync(pageUrl, fields.AsEnumerable(), postUrl);

    public async Task SignInAsync(string email, string password)
    {
        var response = await SubmitFormAsync("/Login", new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = password
        });

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/", response.Headers.Location?.OriginalString);
    }

    public static string PathOf(HttpResponseMessage response)
    {
        var location = response.Headers.Location!;
        return location.IsAbsoluteUri ? location.PathAndQuery : location.OriginalString;
    }
}

public static class DemoUsers
{
    // Seeded by Data/SeedData.cs on start-up.
    public const string EmployeeEmail = "employee@test.local";
    public const string EmployeePassword = "Employee@123";
    public const string DonorEmail = "donor@test.local";
    public const string DonorPassword = "Donor@123";
    public const string VolunteerEmail = "volunteer@test.local";
}
