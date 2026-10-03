using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net;
using GiftOfTheGivers.LoadTests;
using NBomber.Contracts;
using NBomber.Contracts.Stats;
using NBomber.CSharp;

// Usage (from the repo root):
//   dotnet run -c Release --project GiftOfTheGivers.LoadTests -- --target live  --profile load
//   dotnet run -c Release --project GiftOfTheGivers.LoadTests -- --target local --profile stress --label before
//   dotnet run -c Release --project GiftOfTheGivers.LoadTests -- --target local --profile fault --label after
// In Visual Studio: set GiftOfTheGivers.LoadTests as the startup project; the default arguments
// are in Properties/launchSettings.json. The Function key is read from GOTG_FUNCTION_KEY only.

string Arg(string name, string fallback)
{
    var i = Array.IndexOf(args, $"--{name}");
    return i >= 0 && i + 1 < args.Length ? args[i + 1] : fallback;
}

var target = TargetSettings.Load(Arg("target", "local"));
var profile = Arg("profile", "load");
var label = Arg("label", "");
var runName = $"{profile}-{target.Name}{(label.Length > 0 ? "-" + label : "")}-{DateTime.Now:yyyyMMdd-HHmm}";
var reportFolder = Path.Combine(Arg("reports", "reports"), runName);

Console.WriteLine($"Target  : {target.Name} ({target.BaseUrl})");
Console.WriteLine($"Profile : {profile}");
Console.WriteLine($"Function: {(target.HasFunction ? "included (key from GOTG_FUNCTION_KEY)" : "skipped (no GOTG_FUNCTION_KEY / URL)")}");
Console.WriteLine($"Reports : {Path.GetFullPath(reportFolder)}");

var site = new SiteClient(TimeSpan.FromSeconds(30));

// ---- Warm-up -------------------------------------------------------------------------------
// The Azure SQL database is serverless and auto-pauses, and the Function App is on the
// consumption plan, so the very first requests are slow. Wake everything up before measuring.
Console.WriteLine("\nWarm-up (not part of the results):");
await WarmUp($"{target.BaseUrl}/", "home");
await WarmUp($"{target.BaseUrl}/Donate", "donate page");
if (target.HasFunction)
{
    await WarmUp(FunctionUrl(), "tax certificate function", code => code == HttpStatusCode.NotFound, FunctionHeaders());
}

var canSignIn = await TrySignInOnce();
Console.WriteLine(canSignIn
    ? $"Sign-in check: {target.DonorEmail} can sign in, the login + donor dashboard steps are included."
    : $"Sign-in check: {target.DonorEmail} could NOT sign in on this target, so the login + dashboard steps are skipped.");

// ---- Profiles ------------------------------------------------------------------------------
var isStress = profile == "stress";
var isFault = profile == "fault";
var thinkTime = isStress || isFault ? TimeSpan.Zero : TimeSpan.FromSeconds(1);   // real visitors pause; stress does not

// Load: 5 -> 20 -> 50 concurrent users. Stress: keeps adding users until the site degrades.
// Fault: 25 steady users while the local database is killed 20 s in (recovery / retry test).
var stages = isStress
    ? new[] { 10, 25, 50, 75, 100, 150, 200 }.Select(u => (Users: u, Ramp: 10, Hold: 20)).ToArray()
    : isFault
        ? new[] { (Users: 25, Ramp: 5, Hold: 55) }
        : new[] { (Users: 5, Ramp: 10, Hold: 30), (Users: 20, Ramp: 15, Hold: 45), (Users: 50, Ramp: 15, Hold: 45) };

var simulations = stages.SelectMany(s => new[]
{
    Simulation.RampingConstant(copies: s.Users, during: TimeSpan.FromSeconds(s.Ramp)),
    Simulation.KeepConstant(copies: s.Users, during: TimeSpan.FromSeconds(s.Hold))
}).ToArray();

int UsersAt(double seconds)
{
    var t = 0.0;
    var previous = 0;
    foreach (var s in stages)
    {
        if (seconds < t + s.Ramp) return previous + (int)((s.Users - previous) * (seconds - t) / s.Ramp);
        t += s.Ramp;
        if (seconds < t + s.Hold) return s.Users;
        t += s.Hold;
        previous = s.Users;
    }
    return previous;
}

// Every request is also written to raw-requests.csv so each user stage can be analysed and graphed.
var runClock = Stopwatch.StartNew();
var rawRequests = new ConcurrentQueue<string>();

var guard = isStress
    ? new DegradationGuard(UsersAt, maxErrorRate: 0.05, maxP95Ms: 5000, Path.Combine(reportFolder, "stress-timeline.csv"))
    : null;

// ---- Scenario: one visitor journey per iteration ------------------------------------------
var journey = Scenario.Create("visitor_journey", async context =>
{
    var home = await Timed("home", context, () => site.GetAsync($"{target.BaseUrl}/"));
    if (home.IsError) return Stop(context, home);
    await Task.Delay(thinkTime);

    var donate = await Timed("donate_page", context, () => site.GetAsync($"{target.BaseUrl}/Donate"));
    if (donate.IsError) return Stop(context, donate);
    await Task.Delay(thinkTime);

    // The login page is a normal public page, so every visitor opens it.
    var jar = new SiteClient.CookieJar();
    string? token = null;
    var loginPage = await Timed("login_page", context, async () =>
    {
        var r = await site.GetAsync($"{target.BaseUrl}/Login", jar);
        token = SiteClient.AntiforgeryToken(r.Body);
        return r;
    });
    if (loginPage.IsError || token == null) return Stop(context, loginPage);
    await Task.Delay(thinkTime);

    // Signing in and opening the donor dashboard needs a donor account that works on the target.
    if (canSignIn)
    {
        var login = await Timed("login_post", context, async () =>
        {
            var r = await site.PostFormAsync($"{target.BaseUrl}/Login", new Dictionary<string, string>
            {
                ["Email"] = target.DonorEmail,
                ["Password"] = target.DonorPassword,
                ["__RequestVerificationToken"] = token!
            }, jar, code => code == HttpStatusCode.Redirect);

            // A failed login also redirects (back to /Login), so check where it went.
            return r.Location == "/Login"
                ? r with { Response = Response.Fail(statusCode: "login_rejected", message: "Login redirected back to /Login") }
                : r;
        });
        if (login.IsError) return Stop(context, login);
        await Task.Delay(thinkTime);

        var dashboard = await Timed("donor_dashboard", context, () => site.GetAsync($"{target.BaseUrl}/Dashboards/Donor", jar));
        if (dashboard.IsError) return Stop(context, dashboard);
        await Task.Delay(thinkTime);
    }

    if (target.HasFunction && !isStress)
    {
        var fn = await Timed("tax_certificate_function", context,
            () => site.GetAsync(FunctionUrl(), expected: code => code == HttpStatusCode.NotFound, headers: FunctionHeaders()));
        if (fn.IsError) return Stop(context, fn);
        await Task.Delay(thinkTime);
    }

    if (guard?.Check() is { } reason) context.StopCurrentTest($"Stopped at first clear degradation: {reason}");
    return Response.Ok();
})
.WithoutWarmUp()
.WithLoadSimulations(simulations);

// Fault profile (local only): kill the LocalDB instance 20 s in, the same kind of drop-out the app
// sees when the serverless Azure SQL database pauses or fails over. LocalDB starts again on the next
// connection, so the question is whether requests fail (HTTP 500) or ride it out with retries.
if (isFault && target.Name == "local")
{
    _ = Task.Run(async () =>
    {
        await Task.Delay(TimeSpan.FromSeconds(20));
        using var kill = Process.Start(new ProcessStartInfo("sqllocaldb", "stop MSSQLLocalDB -k") { UseShellExecute = false, RedirectStandardOutput = true });
        kill?.WaitForExit();
        Console.WriteLine($"[fault] t={runClock.Elapsed.TotalSeconds:0}s LocalDB instance MSSQLLocalDB killed (exit {kill?.ExitCode})");
    });
}

runClock.Restart();
var stats = NBomberRunner
    .RegisterScenarios(journey)
    .WithTestSuite("GiftOfTheGivers")
    .WithTestName(runName)
    .WithReportFolder(reportFolder)
    .WithReportFileName(runName)
    .WithReportFormats(ReportFormat.Html, ReportFormat.Md, ReportFormat.Csv, ReportFormat.Txt)
    .Run();

Directory.CreateDirectory(reportFolder);
File.WriteAllLines(Path.Combine(reportFolder, "raw-requests.csv"),
    new[] { "elapsed_s,step,latency_ms,ok,status" }.Concat(rawRequests));

// ---- Short summary for the console and docs ------------------------------------------------
var scenario = stats.ScenarioStats[0];
Console.WriteLine($"\n=== {runName} ===");
Console.WriteLine($"Duration {scenario.Duration:mm\\:ss}, requests ok {scenario.Ok.Request.Count}, failed {scenario.Fail.Request.Count}");
Console.WriteLine($"{"step",-26}{"requests",9}{"rps",8}{"p50",8}{"p95",8}{"p99",8}{"max",8}{"err%",7}");
foreach (var step in scenario.StepStats)
{
    var total = step.Ok.Request.Count + step.Fail.Request.Count;
    var errPct = total == 0 ? 0 : 100.0 * step.Fail.Request.Count / total;
    var l = step.Ok.Latency;
    Console.WriteLine($"{step.StepName,-26}{total,9}{step.Ok.Request.RPS + step.Fail.Request.RPS,8:0.0}{l.Percent50,8:0}{l.Percent95,8:0}{l.Percent99,8:0}{l.MaxMs,8:0}{errPct,7:0.0}");
}
if (guard != null)
{
    Console.WriteLine(guard.DegradedReason is { } why
        ? $"Degradation point: {why}"
        : "No clear degradation within the bounded ramp.");
}

return 0;

// ---- Helpers -------------------------------------------------------------------------------
string FunctionUrl() => $"{target.FunctionUrl!.TrimEnd('/')}/{target.UnknownDonationId}";

Dictionary<string, string> FunctionHeaders() => new() { ["x-functions-key"] = target.FunctionKey! };

async Task<Response<object>> Timed(string name, IScenarioContext context, Func<Task<SiteClient.Result>> call)
{
    return await Step.Run(name, context, async () =>
    {
        var sw = Stopwatch.StartNew();
        var result = await call();
        guard?.Record(sw.Elapsed.TotalMilliseconds, !result.Response.IsError, result.Response.StatusCode);
        rawRequests.Enqueue($"{runClock.Elapsed.TotalSeconds:0.000},{name},{sw.Elapsed.TotalMilliseconds:0.0},{(result.Response.IsError ? 0 : 1)},{result.Response.StatusCode}");
        if (result.Response.IsError)
            context.Logger.Warning("{Step} failed: {Status} {Message}", name, result.Response.StatusCode, result.Response.Message);
        return result.Response;
    });
}

Response<object> Stop(IScenarioContext context, Response<object> failed)
{
    if (guard?.Check() is { } reason) context.StopCurrentTest($"Stopped at first clear degradation: {reason}");
    return Response.Fail(statusCode: failed.StatusCode, message: failed.Message);
}

async Task WarmUp(string url, string name, Func<HttpStatusCode, bool>? expected = null, IDictionary<string, string>? headers = null)
{
    for (var attempt = 1; attempt <= 6; attempt++)
    {
        var sw = Stopwatch.StartNew();
        var r = await site.GetAsync(url, expected: expected, headers: headers);
        Console.WriteLine($"  {name,-26} attempt {attempt}: HTTP {r.Response.StatusCode} in {sw.ElapsedMilliseconds} ms");
        if (!r.Response.IsError && sw.ElapsedMilliseconds < 2000) return;
        await Task.Delay(TimeSpan.FromSeconds(5));
    }
}

async Task<bool> TrySignInOnce()
{
    var jar = new SiteClient.CookieJar();
    var page = await site.GetAsync($"{target.BaseUrl}/Login", jar);
    var token = SiteClient.AntiforgeryToken(page.Body);
    if (token == null) return false;
    var post = await site.PostFormAsync($"{target.BaseUrl}/Login", new Dictionary<string, string>
    {
        ["Email"] = target.DonorEmail,
        ["Password"] = target.DonorPassword,
        ["__RequestVerificationToken"] = token
    }, jar, code => code == HttpStatusCode.Redirect);
    if (post.Response.IsError || post.Location == "/Login") return false;
    var dashboard = await site.GetAsync($"{target.BaseUrl}/Dashboards/Donor", jar);
    return !dashboard.Response.IsError;
}
