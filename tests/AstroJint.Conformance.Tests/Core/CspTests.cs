using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/csp.test.ts</c>. The fixtures are <c>csp</c>, <c>csp-fonts</c> and <c>csp-adapter</c> of Astro
/// (<c>core-csp</c>, <c>core-csp-fonts</c> and <c>core-csp-adapter</c>), with <c>security.csp</c> as upstream sets it.
/// <list type="bullet">
/// <item>Seven of the upstream tests build with no adapter and read the <c>&lt;meta http-equiv="Content-Security-Policy"&gt;</c> of the
/// prerendered files from the output directory. Here the same fixtures are built with this adapter (the pages stay prerendered,
/// because the fixtures set no output) and each page is requested from the host, which serves the file. The files are the
/// same ones; the tests do not run the engine.</item>
/// <item>The <c>csp</c> fixture lacks <c>pages/react.astro</c> and <c>components/Text.jsx</c> and the React integration: the test
/// reads none of them. The <c>csp-adapter</c> fixture lacks <c>components/Text.jsx</c>, which no page imports.</item>
/// <item>Upstream builds the <c>csp</c> fixture once per test (each into its own folder); the tests share one build, because they
/// only read different pages. The <c>kind</c> test is a JSON config variant, a second build.</item>
/// <item><c>should return CSP header inside a hook</c> hands the adapter's <c>setRouteToHeaders()</c> hook to the test adapter. This
/// adapter takes the same map in <c>astro:build:done</c> and writes it into <c>astro-jint.json</c> (<c>staticHeaders</c>, keyed by the
/// file of each page) for the host, which sends it as a response header. The test reads that list and requests the four pages, to see
/// that the host sends each header. The fixture turns the adapter's <c>staticHeaders</c> option on.</item>
/// </list>
/// </summary>
[Trait("Upstream", "core/csp.test.ts")]
public partial class CspTests(SiteHost sites)
{
    private const string Fixture = "core-csp";
    private const string FontsFixture = "core-csp-fonts";
    private const string AdapterFixture = "core-csp-adapter";

    private static readonly object GranularDirectives = new
    {
        security = new
        {
            csp = new
            {
                scriptDirective = new { resources = new[] { new { resource = "https://cdn.example.com", kind = "element" } } },
                styleDirective = new { resources = new[] { new { resource = "'unsafe-inline'", kind = "attribute" } } },
            },
        },
    };

    [Fact(DisplayName = "CSP: should generate hashes for inline styles")]
    public async Task InlineStyle_HasItsHashInThePolicy()
    {
        var site = await sites.GetAsync(Fixture);

        var document = await GetPageAsync(site, "/inline/");
        var styleContent = string.Concat(document.QuerySelectorAll("style").Select(style => style.TextContent));

        Assert.Contains($"'{CspDigest.Sha256(styleContent)}'", PolicyOf(document));
    }

    [Fact(DisplayName = "CSP: should generate hashes and directives for fonts")]
    public async Task Fonts_HaveStyleHashesAndTheFontDirective()
    {
        var site = await sites.GetAsync(FontsFixture);

        var document = await GetPageAsync(site, "/");
        var parsed = ParsePolicy(PolicyOf(document));

        Assert.True(parsed.First(entry => entry.Directive == "style-src").Resources.Length == 2, "Style hash is not injected by vite-plugin-fonts");
        Assert.Equal(["'self'", "https://fonts.cdn.test.com"], parsed.First(entry => entry.Directive == "font-src").Resources);
    }

    [Fact(DisplayName = "CSP: should generate hashes for Image component inline styles when using layout")]
    public async Task Image_WithLayout_UsesDataAttributesAndHasStyleHashes()
    {
        var site = await sites.GetAsync(Fixture);

        var document = await GetPageAsync(site, "/image/");

        AssertImageUsesDataAttributes(document.QuerySelector("img"));
        AssertPolicyHasStyleHashes(PolicyOf(document));
    }

    [Fact(DisplayName = "CSP: should generate hashes for Picture component inline styles when using layout")]
    public async Task Picture_WithLayout_UsesDataAttributesAndHasStyleHashes()
    {
        var site = await sites.GetAsync(Fixture);

        var document = await GetPageAsync(site, "/picture/");

        AssertImageUsesDataAttributes(document.QuerySelector("picture img"));
        AssertPolicyHasStyleHashes(PolicyOf(document));
    }

    [Fact(DisplayName = "CSP: should not use inline styles for custom position (CSP compliance)")]
    public async Task Image_WithCustomPosition_HasNoInlineStyle()
    {
        var site = await sites.GetAsync(Fixture);

        var document = await GetPageAsync(site, "/image-position/");
        var img = document.QuerySelector("img");
        var policy = PolicyOf(document);

        Assert.Equal("top", img?.GetAttribute("data-astro-image-pos"));
        var style = img?.GetAttribute("style");
        if (!string.IsNullOrEmpty(style))
        {
            Assert.DoesNotContain("object-position", style);
        }

        Assert.Contains("style-src", policy);
        Assert.DoesNotContain("'unsafe-inline'", policy);
    }

    [Fact(DisplayName = "CSP: should generate hashes for SVG component inline styles")]
    public async Task Svg_InlineStyle_HasItsHashInThePolicy()
    {
        var site = await sites.GetAsync(Fixture);

        var document = await GetPageAsync(site, "/svg/");

        Assert.NotEmpty(document.QuerySelectorAll("svg"));
        var svgStyle = document.QuerySelectorAll("svg style");
        Assert.NotEmpty(svgStyle);
        Assert.Contains(".square{fill: red}", string.Concat(svgStyle.Select(style => style.TextContent)));

        // The style stays inside the svg.
        Assert.DoesNotContain(document.QuerySelectorAll("head style"), style => style.TextContent.Contains(".square{fill: red}"));

        var policy = PolicyOf(document);
        Assert.Contains("style-src", policy);
        Assert.Contains("'sha256-TFjYo91aZcH4Kex6qdJUFz/POAVYu5H/OgkpRfHpLfw='", policy);
    }

    [Fact(DisplayName = "CSP: should support granular directives via the `kind` option")]
    public async Task GranularDirectives_AreAppliedByKind()
    {
        var site = await sites.GetAsync(Fixture, GranularDirectives);

        var document = await GetPageAsync(site, "/inline/");
        var parsed = ParsePolicy(PolicyOf(document))
            .GroupBy(entry => entry.Directive)
            .ToDictionary(group => group.Key, group => group.Last().Resources);

        // Baseline directives are always present.
        Assert.True(parsed.ContainsKey("script-src"), "script-src baseline present");
        Assert.True(parsed.ContainsKey("style-src"), "style-src baseline present");

        // `script-src-elem` carries the user element resource and the hashes of Astro (folded).
        Assert.True(parsed.ContainsKey("script-src-elem"), "script-src-elem present");
        var scriptElem = parsed["script-src-elem"];
        Assert.Contains("https://cdn.example.com", scriptElem);
        Assert.Contains(scriptElem, resource => resource.Contains("sha256-"));

        // `style-src-attr` carries the user attribute source and no folded hashes.
        Assert.True(parsed.ContainsKey("style-src-attr"), "style-src-attr present");
        Assert.Equal(["'unsafe-inline'"], parsed["style-src-attr"]);
    }

    [Fact(DisplayName = "CSP: should return CSP header inside a hook")]
    public async Task StaticHeaders_AreRecordedForThePrerenderedPages_AndServed()
    {
        var site = await sites.GetAsync(AdapterFixture);

        using var manifest = JsonDocument.Parse(await File.ReadAllTextAsync(Path.Combine(site.DistPath, "astro-jint.json")));
        var policies = new Dictionary<string, string>();
        foreach (var entry in manifest.RootElement.GetProperty("staticHeaders").EnumerateArray())
        {
            var file = entry.GetProperty("file").GetString()!;
            Assert.True(entry.GetProperty("headers").TryGetProperty("Content-Security-Policy", out var policy), $"{file} should have a CSP header");
            policies[file] = policy.GetString()!;
        }

        // Four routes: /, /scripts, /foo and /bar (the page of /ssr is on demand).
        Assert.Equal(["bar/index.html", "foo/index.html", "index.html", "scripts/index.html"], policies.Keys.OrderBy(file => file, StringComparer.Ordinal));

        // The host sends each page's header, the one that the hook received.
        foreach (var (path, file) in new[] { ("/", "index.html"), ("/scripts/", "scripts/index.html"), ("/foo/", "foo/index.html"), ("/bar/", "bar/index.html") })
        {
            using var response = await site.GetAsync(path);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal(policies[file], Assert.Single(response.HeaderValues("Content-Security-Policy")));
        }
    }

    private static async Task<IDocument> GetPageAsync(ConformanceSite site, string path)
    {
        using var response = await site.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadHtmlAsync();
    }

    private static string PolicyOf(IDocument document)
    {
        var meta = Assert.Single(document.QuerySelectorAll("meta[http-equiv='Content-Security-Policy']"));
        return meta.GetAttribute("content") ?? throw new InvalidOperationException("The policy has no content.");
    }

    private static List<(string Directive, string[] Resources)> ParsePolicy(string policy) =>
        policy.Split(';')
            .Select(part => part.Trim())
            .Where(part => part.Length > 0)
            .Select(part => part.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
            .Select(words => (words[0], words[1..]))
            .ToList();

    // The image has data attributes instead of inline styles (CSP-compliant).
    private static void AssertImageUsesDataAttributes(IElement? img)
    {
        Assert.NotNull(img);
        Assert.False(string.IsNullOrEmpty(img.GetAttribute("data-astro-image")), "Image should have data-astro-image attribute");
        Assert.False(string.IsNullOrEmpty(img.GetAttribute("data-astro-image-fit")), "Image should have data-astro-image-fit attribute");
        Assert.False(string.IsNullOrEmpty(img.GetAttribute("data-astro-image-pos")), "Image should have data-astro-image-pos attribute");
    }

    // The style-src directive has at least one sha256 hash, for the static CSS.
    private static void AssertPolicyHasStyleHashes(string policy)
    {
        Assert.Contains("style-src", policy);
        Assert.True(StyleHash().IsMatch(policy), "CSP should contain style hashes");
    }

    [GeneratedRegex("sha256-[A-Za-z0-9+/=]+")]
    private static partial Regex StyleHash();
}
