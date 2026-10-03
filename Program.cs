using GiftOfTheGivers.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString, sql =>
        // Azure SQL serverless pauses when idle and the first queries after that fail while it
        // resumes (transient errors such as 40613). Retry those instead of showing an error page.
        // -1 is added because a dropped connection ("physical connection is not usable") came back
        // as error -1 in the fault test and is not on the built-in transient list.
        sql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(10), errorNumbersToAdd: new[] { -1 })));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

// Use custom authentication with the Users table
builder.Services.AddAuthentication("Cookies")
    .AddCookie("Cookies", options =>
    {
        options.LoginPath = "/Login";
        options.AccessDeniedPath = "/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(24);
        options.SlidingExpiration = true;
    });

builder.Services.AddAuthorization();

builder.Services.AddRazorPages();

// Output caching for public, read-heavy pages (the home page runs five database queries per visit).
// Only anonymous GET requests are cached; signed-in users always get a fresh page.
builder.Services.AddOutputCache(options =>
{
    options.AddPolicy("PublicPage", policy => policy.Expire(TimeSpan.FromSeconds(60)).Tag("public"));
});

var app = builder.Build();

// Seed the database with initial data
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        await SeedData.Initialize(services);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred seeding the database.");
    }
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();
app.UseOutputCache();

app.MapRazorPages();

app.Run();

// Exposes the entry point to WebApplicationFactory in the integration tests.
public partial class Program { }
