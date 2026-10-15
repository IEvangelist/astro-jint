using System.Net;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/astro-dev-platform.test.ts</c>. The upstream tests start the dev server
/// of a fixture with <c>platformProxy</c>; they are about what the dev server hands to the pages (the <c>cf</c> object, a
/// mock of <c>caches</c>, D1, R2 and KV bindings) and about the <c>Code</c> and <c>Prism</c> components of Astro, which the
/// dev server must load although some of what they use is CommonJS. The three tests about the components ask the pages
/// <c>/code-test</c> and <c>/prism-test</c> of the shared fixture <c>cf-content</c>, rendered on demand by the host from the
/// server bundle, and the log of the host (<c>Logs</c>) replaces the log of the dev server. The two tests about <c>Prism</c>
/// pass; the test about <c>Code</c> is skipped for a gap.
/// <list type="bullet">
/// <item><c>Prism</c> (GAP-CF-10, fixed by the adapter): <c>@astrojs/prism</c> loads the grammars through the import
/// <c>#prism-loadLanguages</c>, which is <c>loadLanguages-default.js</c> unless the build has the <c>workerd</c> condition. That
/// loader is <c>loadLanguages</c> of <c>prismjs/components/index.js</c>, which neither the bundle nor Jint can run: the library
/// sets the global <c>Prism</c> only if <c>typeof global !== 'undefined'</c> (Node's name for <c>globalThis</c>), which Jint
/// does not have, and the loader calls <c>require.resolve()</c>, which a bundle cannot have. Without the fix the page answers an
/// empty 500 (<c>ReferenceError: Prism is not defined</c>), and a prerendered page with the component fails the build of the
/// whole site. The adapter does in <c>src/prism.ts</c> what <c>@astrojs/cloudflare</c> does in <c>vite-plugin-prism.ts</c>: the
/// plugin <c>astro-adapter-jint:prism</c> resolves <c>#prism-loadLanguages</c> to <c>loadLanguages-workerd.js</c> and provides
/// the virtual module <c>virtual:astro-cloudflare:prism</c> that this loader imports, with one dynamic import for each grammar of
/// prismjs. It is a plugin for these two imports and not the condition itself, which also selects the WebAssembly build of Shiki
/// (<c>shiki/onig.wasm</c>) that cannot be bundled outside of workerd. The test that reads the log asks for the status first:
/// without a page that renders, a log free of "Unable to load the language" would prove nothing.</item>
/// <item>GAP-CF-11, <c>Code</c>: the page answers an empty 500 and the console has <c>ReferenceError: WebAssembly is not
/// defined</c> in <c>getWasmInstance</c>. Shiki, which the component uses, loads its regular expression engine (Oniguruma)
/// as WebAssembly, which Jint does not have. A prerendered page with the component is not affected: the build runs in
/// Node.js, which has WebAssembly.</item>
/// </list>
/// <para>
/// Four extra tests of the host (named "(host)") go with them. Two are about <c>Prism</c>: the page <c>/prism-test</c> has
/// four blocks, for css, js, ts and rust, that are highlighted, and the page <c>/prism-prerendered/</c>, which has
/// <c>prerender = true</c>, is a file of the build with highlighted blocks (this page also guards the build of the whole
/// fixture, which fails without the plugin of the adapter). The third, about <c>Code</c>, checks that the page
/// <c>/code-prerendered/</c> is a file of the build with a highlighted block, so the gap is for on-demand pages only. The
/// fourth shows what the gap of <c>Code</c> needs, with a build
/// variant made by the integration <c>cf-shiki-js-engine</c> of <c>support/integrations.mjs</c>, which resolves
/// <c>#shiki-engine</c> to the regular expression engine of Shiki written in JavaScript; <c>/code-test</c> then renders, but
/// the first request allocates more than 384 MiB (it fails at 384 MiB and passes at 512 MiB), so this host raises
/// <c>MemoryLimitBytes</c> from its default of 128 MiB. The <c>workerd</c> condition itself cannot be added to the build of this
/// host: the build of the whole fixture fails on <c>shiki/onig.wasm</c>, which the <c>Code</c> page selects under that condition.
/// </para>
/// <para>
/// Not applicable: the five tests "adds cf object", "adds cache mocking", "adds D1 mocking", "adds R2 mocking" and "adds
/// KV mocking" ask the Cloudflare dev platform (the proxy of wrangler) for objects that the host does not have.
/// </para>
/// </summary>
[Trait("Upstream", "cloudflare/astro-dev-platform.test.ts")]
public class AstroDevPlatformTests(SiteHost sites)
{
    private const string CodeGap =
        "GAP-CF-11: GET /code-test answers an empty 500, and the console has ReferenceError: WebAssembly is not defined (getWasmInstance of shiki/wasm): Jint has no WebAssembly, which the Oniguruma engine of Shiki needs";

    [Fact(DisplayName = "Code component works in dev mode (no CommonJS module errors)", Skip = CodeGap)]
    public async Task CodeComponent_Works()
    {
        var site = await sites.GetAsync(CfContent.Fixture);

        using var response = await site.GetAsync("/code-test");

        var body = await response.ReadTextAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, CfContent.Describe(site, response, body));
        var document = ConformanceSite.ParseHtml(body);
        Assert.Equal("Testing Code Component", document.QuerySelector("h1")?.TextContent);
        Assert.True(document.QuerySelectorAll("pre").Length > 0, "Code block should be rendered");
    }

    [Fact(DisplayName = "Prism component works in dev mode (no CommonJS module errors)")]
    public async Task PrismComponent_Works()
    {
        var site = await sites.GetAsync(CfContent.Fixture);

        using var response = await site.GetAsync("/prism-test");

        var body = await response.ReadTextAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, CfContent.Describe(site, response, body));
        var document = ConformanceSite.ParseHtml(body);
        Assert.Equal("Testing Prism Component", document.QuerySelector("h1")?.TextContent);
        // css, js, ts and rust
        Assert.Equal(4, document.QuerySelectorAll("pre").Length);
    }

    [Fact(DisplayName = "Prism component loads languages correctly in dev mode")]
    public async Task PrismComponent_LoadsLanguages()
    {
        var site = await sites.GetAsync(CfContent.Fixture);

        using var response = await site.GetAsync("/prism-test");

        var body = await response.ReadTextAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, CfContent.Describe(site, response, body));
        var unableToLoad = site.Logs.FirstOrDefault(entry => entry.Message.Contains("Unable to load the language"));
        Assert.True(unableToLoad is null, $"Should not see \"Unable to load the language\" message, but got: {unableToLoad?.Message}");
    }

    [Fact(DisplayName = "Prism component highlights the blocks of each language (host)")]
    public async Task PrismComponent_HighlightsEachLanguage()
    {
        var site = await sites.GetAsync(CfContent.Fixture);

        using var response = await site.GetAsync("/prism-test");

        var body = await response.ReadTextAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, CfContent.Describe(site, response, body));
        var blocks = ConformanceSite.ParseHtml(body).QuerySelectorAll("pre");
        Assert.Equal(4, blocks.Length);
        Assert.All(blocks, block => Assert.True(block.QuerySelectorAll(".token").Length > 0, $"not highlighted: {block.OuterHtml}"));
    }

    [Fact(DisplayName = "Prism component of a prerendered page is highlighted by the build (host)")]
    public async Task PrismComponent_IsHighlightedWhenPrerendered()
    {
        var site = await sites.GetAsync(CfContent.Fixture);

        // The page is a file of the build, which the host serves without Jint.
        Assert.True(site.PathExists("client/prism-prerendered/index.html"), "the build wrote no prerendered page for the Prism component");
        var document = await site.GetHtmlAsync("/prism-prerendered/");
        Assert.Equal("Testing a Prerendered Prism Component", document.QuerySelector("h1")?.TextContent);
        var blocks = document.QuerySelectorAll("pre");
        Assert.Equal(2, blocks.Length);
        Assert.All(blocks, block => Assert.True(block.QuerySelectorAll(".token").Length > 0, $"not highlighted: {block.OuterHtml}"));
    }

    [Fact(DisplayName = "Code component of a prerendered page is highlighted by the build (host)")]
    public async Task CodeComponent_IsHighlightedWhenPrerendered()
    {
        var site = await sites.GetAsync(CfContent.Fixture);

        // The build prerenders in Node.js, which has the WebAssembly that Shiki needs, and the host serves the file without Jint.
        Assert.True(site.PathExists("client/code-prerendered/index.html"), "the build wrote no prerendered page for the Code component");
        var document = await site.GetHtmlAsync("/code-prerendered/");
        Assert.Equal("Testing a Prerendered Code Component", document.QuerySelector("h1")?.TextContent);
        Assert.True(document.QuerySelectorAll("pre.astro-code span").Length > 0, "Code block should be rendered and highlighted");
    }

    // The engine of Shiki written in JavaScript instead of the Oniguruma engine, which is WebAssembly.
    private static readonly object ShikiWithoutWebAssembly = new
    {
        integrations = new[] { new { conformanceIntegration = "cf-shiki-js-engine" } },
    };

    [Fact(DisplayName = "Code component renders when the build has the JavaScript engine of Shiki (host)")]
    public async Task CodeComponent_RendersWithTheJavaScriptEngine()
    {
        // The first request allocates between 384 and 512 MiB for the engine of Shiki, and the host stops a request at 128 MiB.
        var site = await sites.GetAsync(
            CfContent.Fixture,
            ShikiWithoutWebAssembly,
            options => options.MemoryLimitBytes = 512L * 1024 * 1024,
            variant: "memory-limit-512mib");

        using var response = await site.GetAsync("/code-test");

        var body = await response.ReadTextAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, CfContent.Describe(site, response, body));
        var document = ConformanceSite.ParseHtml(body);
        Assert.Equal("Testing Code Component", document.QuerySelector("h1")?.TextContent);
        Assert.True(document.QuerySelectorAll("pre.astro-code span").Length > 0, "Code block should be rendered and highlighted");
    }
}
