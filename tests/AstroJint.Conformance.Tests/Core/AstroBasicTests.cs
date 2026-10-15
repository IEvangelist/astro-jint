using System.Net;
using System.Text.Json;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/astro-basic.test.ts</c> on the <c>astro-basic</c> fixture (<c>core-astro-basic</c>: the sources are the
/// upstream ones, plus the <c>url</c>, <c>prerendered</c> and <c>import-app</c> pages that other ports of this folder use; the switches
/// of its config are documented there). The same fixture is built three ways: static output (the default, as upstream), <c>output: 'server'</c>
/// and, for the custom prerenderer, with the <c>conformance.customPrerenderer</c> switch that adds the copy of <c>test-prerenderer.ts</c>.
/// <para>
/// "Astro basic build": upstream builds with no adapter and reads files from the output directory (<c>fixture.readFile('/x/index.html')</c>);
/// the port builds with this adapter, so the same pages are served by the host and each <c>readFile</c> is <c>GET /x/</c>. The "preview" tests
/// are ADAPTED: the preview server is replaced by the host, so the requests are the same ones to the host. "server sourcemaps not included
/// in output" is ADAPTED: the fixture has <c>vite.build.sourcemap: true</c>, so the server bundle has maps. Upstream deletes the server build
/// of a static site and lists the root of the output, which is the site; here the site is <c>dist/client</c> and the server bundle stays in
/// <c>dist/server</c>, with its maps, and the host never serves it. The test lists the <c>dist</c> root as upstream does, and also
/// checks that <c>dist/client</c> only has maps of client scripts and that the host does not answer a request for a server map.
/// </para>
/// <para>
/// "Astro basic development": the dev server is replaced by a production build of the same project with <c>output: 'server'</c>, so both
/// tests are ADAPTED. They request the pages the dev server serves, which are on-demand in that build. The dev server maps
/// <c>/import-queries/raw/index.html</c> to the page; the production app does not, so the request is <c>/import-queries/raw/</c>, and upstream
/// does not look at the scripts and styles that the dev server adds, as the port does not either.
/// </para>
/// <para>
/// "Astro custom prerenderer": GAP-CORE-4. A user integration that calls <c>setPrerenderer</c> cannot be built together with this adapter,
/// so the three upstream tests are skipped. Astro leaves the <c>prerender</c> environment with no input for an adapter that is not a legacy
/// one (<c>entrypointResolution</c> other than <c>explicit</c>) when the settings have a custom prerenderer, and the build of this adapter, which
/// is <c>auto</c>, fails with Vite's "rolldownOptions.input should not be an html file when building for SSR". Not upstream: the three
/// tests with "with its own prerender entry" run the same checks when the fixture adds <c>astro/entrypoints/prerender</c> as the input
/// of that environment (the <c>conformance.prerenderEntry</c> switch), which shows that the prerenderer does everything else it has to.
/// The recorder writes <c>prerenderer-calls.json</c> to the directory that <c>astro:build:done</c> gets, which is <c>dist/client</c> here.
/// </para>
/// </summary>
[Trait("Upstream", "core/astro-basic.test.ts")]
public class AstroBasicTests(SiteHost sites)
{
    private const string Fixture = "core-astro-basic";

    private static readonly object ServerOutput = new { output = "server" };

    private static readonly object CustomPrerenderer = new { conformance = new { customPrerenderer = true } };

    private static readonly object CustomPrerendererWithItsOwnEntry = new { conformance = new { customPrerenderer = true, prerenderEntry = true } };

    private const string GapCustomPrerenderer =
        "GAP-CORE-4: the build fails when an integration calls setPrerenderer, because Astro gives the prerender environment no input for an entrypointResolution 'auto' adapter";

    [Fact(DisplayName = "Astro basic build: Allows scoped classes with spread")]
    public async Task SpreadAttributes_OnScopedElements_KeepTheScopeClass()
    {
        var site = await sites.GetAsync(Fixture);

        var html = await GetHtml(site, "/spread-scope/");

        foreach (var id in new[] { "spread-plain", "spread-class", "spread-class-list" })
        {
            var element = Assert.Single(html.QuerySelectorAll($"#{id}"));
            Assert.Matches("astro-.*", element.GetAttribute("class"));
        }
    }

    [Fact(DisplayName = "Astro basic build: supports special chars in filename")]
    public async Task PageWithSpecialCharsInItsFileName_IsServed()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/special-%E2%80%9Ccharacters%E2%80%9D%20-in-file/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotEmpty(await response.ReadTextAsync());
    }

    [Fact(DisplayName = "Astro basic build: renders markdown in utf-8 by default")]
    public async Task MarkdownPage_IsRenderedInUtf8()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/chinese-encoding-md/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.ReadTextAsync();
        Assert.Equal("我的第一篇博客文章", ConformanceSite.ParseHtml(text).QuerySelector("h1")?.TextContent);
        Assert.Matches("<meta charset=\"utf-8\"", text);
    }

    [Fact(DisplayName = "Astro basic build: Generates pages that end with .mjs")]
    public async Task Endpoints_WithMjsAndJsExtensions_AreGenerated()
    {
        var site = await sites.GetAsync(Fixture);

        foreach (var path in new[] { "/get-static-paths-with-mjs/example.mjs", "/get-static-paths-with-mjs/example.js" })
        {
            using var response = await site.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.NotEmpty(await response.ReadTextAsync());
        }
    }

    [Fact(DisplayName = "Astro basic build: allows file:// urls as module specifiers")]
    public async Task FileUrlModuleSpecifier_IsImported()
    {
        var site = await sites.GetAsync(Fixture);

        var html = await GetHtml(site, "/fileurl/");

        Assert.Equal("WORKS", html.QuerySelector("h1")?.TextContent);
    }

    [Fact(DisplayName = "Astro basic build: Handles importing .astro?raw correctly")]
    public async Task AstroFileImportedWithRaw_IsTheSourceText_AndHoistsNothing()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/import-queries/raw/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.ReadTextAsync();
        var rawValue = ConformanceSite.ParseHtml(text).QuerySelector(".raw-value")?.TextContent ?? "";
        Assert.Matches("<h1>Hello</h1>", rawValue);
        Assert.Matches("<script>", rawValue);
        Assert.Matches("<style>", rawValue);
        // The rest of HTML should not contain any scripts or styles hoisted from the raw import.
        var otherHtml = text.Replace(rawValue, "");
        Assert.DoesNotMatch("<script", otherHtml);
        Assert.DoesNotMatch("<style", otherHtml);
    }

    [Fact(DisplayName = "Astro basic build: server sourcemaps not included in output")]
    public async Task ServerSourcemaps_AreNotInTheSite()
    {
        var site = await sites.GetAsync(Fixture);

        // readdir('/') of the output root: dist/ here, which has the client and the server directories and astro-jint.json.
        Assert.DoesNotContain(Directory.EnumerateFiles(site.DistPath), file => file.EndsWith(".map", StringComparison.Ordinal));

        // Not asserted upstream: the site is dist/client, which only has maps of the scripts it serves, and the host does not serve the server's.
        var clientMaps = Directory.EnumerateFiles(Path.Combine(site.DistPath, "client"), "*.map", SearchOption.AllDirectories);
        Assert.All(clientMaps, map =>
            Assert.True(map.EndsWith(".js.map", StringComparison.Ordinal) && File.Exists(map[..^".map".Length]), $"{map} is not the map of a client script"));
        using var response = await site.GetAsync("/entry.mjs.map");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "Astro basic build: Defines Astro.generator")]
    public async Task Page_HasTheGeneratorMeta()
    {
        var site = await sites.GetAsync(Fixture);

        var html = await GetHtml(site, "/generator/");

        Assert.Matches("^Astro v", html.QuerySelector("meta[name=\"generator\"]")?.GetAttribute("content"));
    }

    [Fact(DisplayName = "Astro basic build: preview: returns 200 for valid URLs")]
    public async Task Host_ValidUrl_Returns200()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "Astro basic build: preview: returns 404 for invalid URLs")]
    public async Task Host_InvalidUrl_Returns404()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/bad-url");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact(DisplayName = "Astro basic development: Renders markdown in utf-8 by default")]
    public async Task OnDemandMarkdownPage_IsRenderedInUtf8()
    {
        var site = await sites.GetAsync(Fixture, ServerOutput);

        using var response = await site.GetAsync("/chinese-encoding-md");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.ReadTextAsync();
        Assert.Equal("我的第一篇博客文章", ConformanceSite.ParseHtml(text).QuerySelector("h1")?.TextContent);
        Assert.DoesNotMatch("charset=utf-8", string.Join(", ", response.HeaderValues("Content-Type")));
        Assert.Matches("<meta charset=\"utf-8\"", text);
    }

    [Fact(DisplayName = "Astro basic development: Handles importing .astro?raw correctly")]
    public async Task OnDemandAstroFileImportedWithRaw_IsTheSourceText_AndHoistsNothing()
    {
        var site = await sites.GetAsync(Fixture, ServerOutput);

        using var response = await site.GetAsync("/import-queries/raw/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var text = await response.ReadTextAsync();
        var rawValue = ConformanceSite.ParseHtml(text).QuerySelector(".raw-value")?.TextContent ?? "";
        Assert.Matches("<h1>Hello</h1>", rawValue);
        Assert.Matches("<script>", rawValue);
        Assert.Matches("<style>", rawValue);
        // The rest of HTML should not contain any scripts or styles hoisted from the raw import.
        Assert.DoesNotMatch(@"_content.astro\?astro&type=style", text);
        Assert.DoesNotMatch(@"_content.astro\?astro&type=script", text);
    }

    [Fact(DisplayName = "Astro custom prerenderer: calls prerenderer lifecycle methods", Skip = GapCustomPrerenderer)]
    public async Task CustomPrerenderer_Build_CallsTheLifecycleMethods() =>
        await AssertLifecycleCalls(CustomPrerenderer);

    [Fact(DisplayName = "Astro custom prerenderer: renders pages through the custom prerenderer", Skip = GapCustomPrerenderer)]
    public async Task CustomPrerenderer_Build_RendersThePages() =>
        await AssertPagesRendered(CustomPrerenderer);

    [Fact(DisplayName = "Astro custom prerenderer: produces valid output", Skip = GapCustomPrerenderer)]
    public async Task CustomPrerenderer_Build_ProducesValidOutput() =>
        await AssertValidOutput(CustomPrerenderer);

    [Fact(DisplayName = "Not upstream: Astro custom prerenderer: calls prerenderer lifecycle methods, with its own prerender entry")]
    public async Task CustomPrerendererWithItsOwnEntry_Build_CallsTheLifecycleMethods() =>
        await AssertLifecycleCalls(CustomPrerendererWithItsOwnEntry);

    [Fact(DisplayName = "Not upstream: Astro custom prerenderer: renders pages through the custom prerenderer, with its own prerender entry")]
    public async Task CustomPrerendererWithItsOwnEntry_Build_RendersThePages() =>
        await AssertPagesRendered(CustomPrerendererWithItsOwnEntry);

    [Fact(DisplayName = "Not upstream: Astro custom prerenderer: produces valid output, with its own prerender entry")]
    public async Task CustomPrerendererWithItsOwnEntry_Build_ProducesValidOutput() =>
        await AssertValidOutput(CustomPrerendererWithItsOwnEntry);

    private async Task AssertLifecycleCalls(object config)
    {
        var site = await sites.GetAsync(Fixture, config);

        using var recorded = ReadRecordedCalls(site);

        var calls = recorded.RootElement.GetProperty("calls");
        Assert.True(calls.GetProperty("setup").GetInt32() == 1, "setup should be called once");
        Assert.True(calls.GetProperty("getStaticPaths").GetInt32() == 1, "getStaticPaths should be called once");
        Assert.True(calls.GetProperty("render").GetInt32() > 0, "render should be called at least once");
        Assert.True(calls.GetProperty("teardown").GetInt32() == 1, "teardown should be called once");
    }

    private async Task AssertPagesRendered(object config)
    {
        var site = await sites.GetAsync(Fixture, config);

        using var recorded = ReadRecordedCalls(site);

        var renderedPaths = recorded.RootElement.GetProperty("renderedPaths").EnumerateArray().Select(path => path.GetString()).ToList();
        Assert.True(renderedPaths.Contains("/"), "should render index page");
        Assert.True(renderedPaths.Count > 0, "should render at least one page");
    }

    private async Task AssertValidOutput(object config)
    {
        var site = await sites.GetAsync(Fixture, config);

        var html = await GetHtml(site, "/");

        Assert.Equal("Hello world!", html.QuerySelector("h1")?.TextContent);
    }

    private static JsonDocument ReadRecordedCalls(ConformanceSite site) =>
        JsonDocument.Parse(File.ReadAllText(Path.Combine(site.DistPath, "client", "prerenderer-calls.json")));

    private static async Task<AngleSharp.Dom.IDocument> GetHtml(ConformanceSite site, string path)
    {
        using var response = await site.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadHtmlAsync();
    }
}
