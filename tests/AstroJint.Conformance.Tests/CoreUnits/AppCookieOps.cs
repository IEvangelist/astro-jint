using System.Text.Json;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// The <c>cookie-ops</c> endpoint of the <c>app-cookies</c> fixture: it runs steps (<c>get</c>, <c>has</c>, <c>set</c>,
/// <c>delete</c>, <c>merge</c>, <c>headers</c>, <c>roundtrip</c>, <c>markResponseSent</c>) on the <c>Astro.cookies</c> of the
/// request, the calls of the unit tests of cookies (<c>packages/astro/test/units/cookies</c>), and answers with what each step gave.
/// </summary>
internal static class AppCookieOps
{
    private const string Fixture = "app-cookies";

    /// <summary>
    /// Runs the steps (anonymous objects that become the JSON the endpoint reads) on the cookies of a request that sends
    /// <paramref name="cookie"/> as its <c>Cookie</c> header (<c>null</c>: no such header). Every run also checks that the
    /// <c>Set-Cookie</c> lines of the response are the strings that <c>cookies.headers()</c> gave at the end of the endpoint:
    /// one line per cookie, in the same order.
    /// </summary>
    public static async Task<CookieRun> RunCookieOpsAsync(this SiteHost sites, string? cookie, params object[] steps)
    {
        var site = await sites.GetAsync(Fixture);
        var target = $"/cookie-ops?steps={Uri.EscapeDataString(JsonSerializer.Serialize(steps))}";
        (string Name, string Value)[] headers = cookie is null ? [] : [("Cookie", cookie)];

        var response = await site.SendRawAsync(target, "GET", null, headers);

        if (response.StatusCode != 200)
        {
            Assert.Fail($"Status {response.StatusCode}: {response.Text}");
        }

        var body = JsonDocument.Parse(response.Text).RootElement;
        var setCookies = response.HeaderValues("Set-Cookie");
        Assert.Equal(body.Strings("headers"), setCookies);
        return new CookieRun(body.GetProperty("results"), setCookies);
    }
}

/// <summary>The answer of the <c>cookie-ops</c> endpoint: the result of each step and the <c>Set-Cookie</c> lines of the response.</summary>
internal sealed class CookieRun(JsonElement results, IReadOnlyList<string> setCookies)
{
    public IReadOnlyList<string> SetCookies => setCookies;

    /// <summary>The result of a step; it fails when the step threw.</summary>
    public JsonElement Result(int step)
    {
        var result = results[step];
        if (result.TryGetProperty("error", out var error))
        {
            Assert.Fail($"The step {step} threw: {error.GetString()}");
        }

        return result;
    }

    /// <summary>The value that a <c>get</c> step found, <c>null</c> when there is no such cookie.</summary>
    public string? Value(int step)
    {
        var result = Result(step);
        return result.Bool("found") ? result.Str("value") : null;
    }

    /// <summary>The answer of a <c>has</c> step.</summary>
    public bool Has(int step) => Result(step).Bool("has");

    /// <summary>The name of the error that a step threw, <c>null</c> when it did not throw.</summary>
    public string? Error(int step) =>
        results[step].TryGetProperty("error", out var error) ? error.GetString() : null;
}
