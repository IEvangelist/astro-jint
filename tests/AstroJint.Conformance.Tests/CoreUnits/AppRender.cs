using System.Text.Json;
using AngleSharp.Dom;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// The <c>app-render</c> fixture and what its tests share: the build that has a <c>site</c> and a <c>base</c>, the parsed HTML of
/// a response, and the probes (GET <c>/probe/&lt;name&gt;</c>, <c>src/pages/probe/[name].ts</c>), the endpoint that runs the
/// calls of the unit tests of the runtime helpers (<c>astro/runtime/server</c>) inside the host and answers with what they gave.
/// </summary>
internal static class AppRender
{
    public const string Fixture = "app-render";

    // The unit tests of Astro that configure the app with `site` and `base` (astro-global). A request has to be under the base.
    public static readonly object SiteAndBase = new { site = "https://mysite.dev/subsite/", @base = "/blog" };

    public static Task<ConformanceSite> GetSiteAndBaseSiteAsync(this SiteHost sites) => sites.GetAsync(Fixture, SiteAndBase);

    public static IDocument Html(RawResponse response) => ConformanceSite.ParseHtml(response.Text);

    public static async Task<Probe> ProbeAsync(this ConformanceSite site, string name)
    {
        var response = await site.SendRawAsync($"/probe/{name}");
        if (response.StatusCode != 200)
        {
            Assert.Fail($"Status {response.StatusCode}: {response.Text}");
        }

        return new Probe(JsonDocument.Parse(response.Text).RootElement);
    }
}

/// <summary>The cases of one probe: each one is <c>{ "value": … }</c> or, when the call threw, <c>{ "error": "…" }</c>.</summary>
internal sealed class Probe(JsonElement cases)
{
    public JsonElement this[string key]
    {
        get
        {
            if (!cases.TryGetProperty(key, out var result))
            {
                Assert.Fail($"The probe has no case '{key}'.");
            }

            if (result.TryGetProperty("error", out var error))
            {
                Assert.Fail($"The case '{key}' threw: {error.GetString()}");
            }

            return result.GetProperty("value");
        }
    }

    public string Str(string key) => this[key].GetString() ?? "";

    public bool Bool(string key) => this[key].GetBoolean();

    public string[] Strings(string key) => [.. this[key].EnumerateArray().Select(item => item.GetString() ?? "")];

    public void AssertStrings(string key, params string[] expected) => Assert.Equal(expected, Strings(key));
}
