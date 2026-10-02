using GiftOfTheGivers.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace GiftOfTheGivers.IntegrationTests.Infrastructure;

/// <summary>
/// Runs the real web app in memory. Azure SQL is swapped for an in-memory SQLite database,
/// so every test class starts with a fresh database that SeedData fills with the demo users.
/// </summary>
public class GiftOfTheGiversWebFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection = new("DataSource=:memory:");

    public GiftOfTheGiversWebFactory()
    {
        // The in-memory database lives as long as this connection stays open.
        _connection.Open();
    }

    /// <summary>The current test's output, so server logs show up next to each test result.</summary>
    public ITestOutputHelper? Output { get; set; }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
            logging.AddProvider(new TestOutputLoggerProvider(() => Output));
            logging.SetMinimumLevel(LogLevel.Information);
            logging.AddFilter("Microsoft", LogLevel.Warning);
            // Keep one line per HTTP request so each test shows the pages it visited.
            logging.AddFilter("Microsoft.AspNetCore.Hosting.Diagnostics", LogLevel.Information);
            logging.AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
            services.AddDbContext<ApplicationDbContext>(options => options
                .UseSqlite(_connection)
                .ReplaceService<IModelCustomizer, SqliteDecimalModelCustomizer>());
        });
    }

    public HttpClient CreateBrowserClient() =>
        CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            BaseAddress = new Uri("https://localhost")
        });

    /// <summary>Opens a scope on the app's own services to read or seed the test database.</summary>
    public async Task<T> WithDbAsync<T>(Func<ApplicationDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        return await action(db);
    }

    public Task WithDbAsync(Func<ApplicationDbContext, Task> action) =>
        WithDbAsync(async db => { await action(db); return true; });

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
        }
    }
}
