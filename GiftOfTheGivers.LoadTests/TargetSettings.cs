using System.Text.Json;

namespace GiftOfTheGivers.LoadTests;

/// <summary>
/// Where the test points and how it signs in. Values come from loadsettings.json and can be
/// overridden with environment variables. The Function key is only ever read from the
/// GOTG_FUNCTION_KEY environment variable, so it never ends up in the repo.
/// </summary>
public sealed record TargetSettings(
    string Name,
    string BaseUrl,
    string? FunctionUrl,
    string? FunctionKey,
    string DonorEmail,
    string DonorPassword,
    int UnknownDonationId)
{
    public bool HasFunction => !string.IsNullOrWhiteSpace(FunctionUrl) && !string.IsNullOrWhiteSpace(FunctionKey);

    public static TargetSettings Load(string target)
    {
        var path = Path.Combine(AppContext.BaseDirectory, "loadsettings.json");
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var root = json.RootElement;

        if (!root.GetProperty("Targets").TryGetProperty(target, out var t))
        {
            throw new ArgumentException($"Unknown target '{target}'. Use 'live' or 'local'.");
        }

        string? Env(string name) => Environment.GetEnvironmentVariable(name) is { Length: > 0 } v ? v : null;

        return new TargetSettings(
            target,
            (Env("GOTG_BASE_URL") ?? t.GetProperty("BaseUrl").GetString()!).TrimEnd('/'),
            Env("GOTG_FUNCTION_URL") ?? t.GetProperty("FunctionUrl").GetString(),
            Env("GOTG_FUNCTION_KEY"),
            Env("GOTG_DONOR_EMAIL") ?? root.GetProperty("DonorEmail").GetString()!,
            Env("GOTG_DONOR_PASSWORD") ?? root.GetProperty("DonorPassword").GetString()!,
            root.GetProperty("UnknownDonationId").GetInt32());
    }
}
