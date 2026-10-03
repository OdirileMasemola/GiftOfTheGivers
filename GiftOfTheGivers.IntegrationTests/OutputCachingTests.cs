using System.Net;
using GiftOfTheGivers.IntegrationTests.Infrastructure;
using Xunit.Abstractions;

namespace GiftOfTheGivers.IntegrationTests;

/// <summary>
/// The home page is output cached for anonymous visitors (stress test mitigation).
/// These tests make sure the cache is used for the public page but never for signed-in users.
/// </summary>
public class OutputCachingTests : IClassFixture<GiftOfTheGiversWebFactory>
{
    private readonly GiftOfTheGiversWebFactory _factory;

    public OutputCachingTests(GiftOfTheGiversWebFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _factory.Output = output;
    }

    [Fact]
    public async Task HomePage_IsServedFromTheCache_ForAnonymousVisitors()
    {
        var client = _factory.CreateBrowserClient();

        var first = await client.GetAsync("/");
        var second = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        // The output cache middleware adds an Age header when it answers from the cache.
        Assert.NotNull(second.Headers.Age);
        Assert.Equal(await first.Content.ReadAsStringAsync(), await second.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task HomePage_IsNotCached_ForSignedInUsers()
    {
        var donor = new BrowserSession(_factory.CreateBrowserClient());
        await donor.SignInAsync(DemoUsers.DonorEmail, DemoUsers.DonorPassword);

        await donor.Client.GetAsync("/");
        var second = await donor.Client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Null(second.Headers.Age);
    }

    [Fact]
    public async Task DonatePage_IsNotCached_BecauseItHasAnAntiforgeryForm()
    {
        var client = _factory.CreateBrowserClient();

        await client.GetAsync("/Donate");
        var second = await client.GetAsync("/Donate");

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Null(second.Headers.Age);
    }
}
