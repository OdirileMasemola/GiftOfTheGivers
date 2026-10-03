using System.Net;
using System.Text.RegularExpressions;
using NBomber.CSharp;
using NBomber.Contracts;

namespace GiftOfTheGivers.LoadTests;

/// <summary>
/// Thin wrapper around one shared HttpClient. Each virtual user keeps its own cookies
/// (antiforgery + auth cookie) in a <see cref="CookieJar"/>, so users never share a session.
/// Every call is turned into an NBomber response with the HTTP status, size and an error type.
/// </summary>
public sealed partial class SiteClient
{
    private readonly HttpClient _http;

    public SiteClient(TimeSpan timeout)
    {
        var handler = new SocketsHttpHandler
        {
            UseCookies = false,                 // cookies are tracked per virtual user instead
            AllowAutoRedirect = false,          // so we can check where login sends us
            PooledConnectionLifetime = TimeSpan.FromMinutes(2),
            MaxConnectionsPerServer = 512,
            AutomaticDecompression = DecompressionMethods.All
        };
        _http = new HttpClient(handler) { Timeout = timeout };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd("GiftOfTheGivers-LoadTest/1.0 (APPR6312 POE test traffic)");
    }

    public sealed class CookieJar
    {
        private readonly Dictionary<string, string> _cookies = new();

        public void Store(HttpResponseMessage response)
        {
            if (!response.Headers.TryGetValues("Set-Cookie", out var values)) return;
            foreach (var value in values)
            {
                var pair = value.Split(';', 2)[0];
                var eq = pair.IndexOf('=');
                if (eq > 0) _cookies[pair[..eq]] = pair[(eq + 1)..];
            }
        }

        public void Apply(HttpRequestMessage request)
        {
            if (_cookies.Count > 0)
                request.Headers.Add("Cookie", string.Join("; ", _cookies.Select(c => $"{c.Key}={c.Value}")));
        }
    }

    /// <summary>Result of one request: the NBomber response plus the body for follow-up steps.</summary>
    public sealed record Result(Response<object> Response, string Body, string? Location);

    public Task<Result> GetAsync(string url, CookieJar? jar = null, Func<HttpStatusCode, bool>? expected = null,
        IDictionary<string, string>? headers = null) =>
        SendAsync(() =>
        {
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            if (headers != null)
                foreach (var h in headers) request.Headers.TryAddWithoutValidation(h.Key, h.Value);
            return request;
        }, jar, expected);

    public Task<Result> PostFormAsync(string url, IDictionary<string, string> form, CookieJar jar,
        Func<HttpStatusCode, bool>? expected = null) =>
        SendAsync(() => new HttpRequestMessage(HttpMethod.Post, url) { Content = new FormUrlEncodedContent(form) },
            jar, expected);

    private async Task<Result> SendAsync(Func<HttpRequestMessage> build, CookieJar? jar, Func<HttpStatusCode, bool>? expected)
    {
        expected ??= code => code == HttpStatusCode.OK;
        using var request = build();
        jar?.Apply(request);

        try
        {
            using var response = await _http.SendAsync(request);
            var body = await response.Content.ReadAsStringAsync();
            jar?.Store(response);

            var code = ((int)response.StatusCode).ToString();
            var size = body.Length;
            var location = response.Headers.Location?.OriginalString;
            return expected(response.StatusCode)
                ? new Result(Response.Ok(statusCode: code, sizeBytes: size), body, location)
                : new Result(Response.Fail(statusCode: code, message: $"Unexpected HTTP {code}", sizeBytes: size), body, location);
        }
        catch (TaskCanceledException)
        {
            return new Result(Response.Fail(statusCode: "timeout", message: "Request timed out"), "", null);
        }
        catch (HttpRequestException ex)
        {
            return new Result(Response.Fail(statusCode: "connection_error", message: ex.Message), "", null);
        }
    }

    public static string? AntiforgeryToken(string html) =>
        TokenRegex().Match(html) is { Success: true } m ? m.Groups[1].Value : null;

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();
}
