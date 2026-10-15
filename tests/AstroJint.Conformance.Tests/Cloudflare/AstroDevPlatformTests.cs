using System.Net;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/astro-dev-platform.test.ts</c>. The upstream tests start the dev server
/// of a fixture with <c>platformProxy</c>; they are about what the dev server hands to the pages (the <c>cf</c> object, a
/// mock of <c>caches</c>, D1, R2 and KV bindings) and about the <c>Code</c> and <c>Prism</c> components of Astro, which the
/// dev server must load although some of what they use is CommonJS. The three tests about the components ask the pages
/// <c>/code-test</c> and <c>/prism-test</c> of the shared fixture <c>cf-content</c>, rendered on demand by the host from the
/// server bundle, and the log of the host (<c>Logs</c>) replaces the log of the dev server. Neither component renders in the
/// host, and the three tests are skipped for two gaps.
/// <list type="bullet">
/// <item>GAP-CF-10, <c>Prism</c>: the page answers an empty 500 and the console has <c>ReferenceError: Prism is not defined</c>
/// in <c>loadLanguages</c> of <c>prismjs/components/index.js</c>, the loader that <c>@astrojs/prism</c> uses unless the build
/// has the <c>workerd</c> condition (it then takes <c>loadLanguages-workerd.js</c> and the virtual module
/// <c>virtual:astro-cloudflare:prism</c> that the Cloudflare adapter provides). The library sets the global <c>Prism</c> only
/// if <c>typeof global !== 'undefined'</c> (Node's name for <c>globalThis</c>), which Jint does not have. Even with <c>global</c>
/// defined (a build with <c>vite.define.global = "globalThis"</c>) the next error is <c>TypeError: Property 'resolve' of object
/// is not a function</c>, the <c>require.resolve()</c> of the loader, which a bundle cannot have. The test that reads the log
/// asks for the status first: without a page that renders, a log free of "Unable to load the language" would prove nothing.</item>
/// <item>GAP-CF-11, <c>Code</c>: the page answers an empty 500 and the console has <c>ReferenceError: WebAssembly is not
/// defined</c> in <c>getWasmInstance</c>. Shiki, which the component uses, loads its regular expression engine (Oniguruma)
/// as WebAssembly, which Jint does not have.</item>
/// </list>
/// <para>
/// Two extra tests of the host (named "(host)") show what each gap needs, with a build variant made by an integration of
/// <c>support/integrations.mjs</c> that answers one import of the build. <c>cf-prism-workerd</c> resolves
/// <c>#prism-loadLanguages</c> to the loader for workers of <c>@astrojs/prism</c> and provides the virtual module that this
/// loader imports; <c>/prism-test</c> then renders four highlighted blocks. <c>cf-shiki-js-engine</c> resolves
/// <c>#shiki-engine</c> to the regular expression engine of Shiki written in JavaScript; <c>/code-test</c> then renders, but
/// the first request allocates more than 384 MiB (it fails at 384 MiB and passes at 512 MiB), so this host raises
/// <c>MemoryLimitBytes</c> from its default of 128 MiB. The <c>workerd</c> condition itself cannot be added to the build of this
/// host: the build of the whole fixture fails, first on the virtual module of Prism, which only the Cloudflare adapter
/// provides, and then on <c>shiki/onig.wasm</c>, which the <c>Code</c> page selects under that condition.
/// </para>
/// <para>
/// Not applicable: the five tests "adds cf object", "adds cache mocking", "adds D1 mocking", "adds R2 mocking" and "adds
/// KV mocking" ask the Cloudflare dev platform (the proxy of wrangler) for objects that the host does not have.
/// </para>
/// </summary>
[Trait("Upstream", "cloudflare/astro-dev-platform.test.ts")]
public class AstroDevPlatformTests(SiteHost sites)
{
    private const string PrismGap =
        "GAP-CF-10: GET /prism-test answers an empty 500, and the console has ReferenceError: Prism is not defined (loadLanguages of prismjs/components/index.js): prismjs sets the global Prism only when typeof global !== 'undefined', and Jint has no global; with global defined the next error is TypeError: Property 'resolve' of object is not a function (require.resolve in the bundle)";

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

    [Fact(DisplayName = "Prism component works in dev mode (no CommonJS module errors)", Skip = PrismGap)]
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

    [Fact(DisplayName = "Prism component loads languages correctly in dev mode", Skip = PrismGap)]
    public async Task PrismComponent_LoadsLanguages()
    {
        var site = await sites.GetAsync(CfContent.Fixture);

        using var response = await site.GetAsync("/prism-test");

        var body = await response.ReadTextAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, CfContent.Describe(site, response, body));
        var unableToLoad = site.Logs.FirstOrDefault(entry => entry.Message.Contains("Unable to load the language"));
        Assert.True(unableToLoad is null, $"Should not see \"Unable to load the language\" message, but got: {unableToLoad?.Message}");
    }

    // What the Cloudflare adapter does for Prism, for this one import: @astrojs/prism has a loader for workers behind the
    // condition "workerd", and the adapter provides the virtual module that this loader imports.
    private static readonly object PrismForWorkers = new
    {
        integrations = new[] { new { conformanceIntegration = "cf-prism-workerd" } },
    };

    [Fact(DisplayName = "Prism component renders when the build has the loader of @astrojs/prism for workers (host)")]
    public async Task PrismComponent_RendersWithTheLoaderForWorkers()
    {
        var site = await sites.GetAsync(CfContent.Fixture, PrismForWorkers);

        using var response = await site.GetAsync("/prism-test");

        var body = await response.ReadTextAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, CfContent.Describe(site, response, body));
        var blocks = ConformanceSite.ParseHtml(body).QuerySelectorAll("pre");
        Assert.Equal(4, blocks.Length);
        Assert.All(blocks, block => Assert.True(block.QuerySelectorAll(".token").Length > 0, $"not highlighted: {block.OuterHtml}"));
        var unableToLoad = site.Logs.FirstOrDefault(entry => entry.Message.Contains("Unable to load the language"));
        Assert.True(unableToLoad is null, $"Should not see \"Unable to load the language\" message, but got: {unableToLoad?.Message}");
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
