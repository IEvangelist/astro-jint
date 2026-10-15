namespace AstroJint.Conformance.CoreBuild;

/// <summary>
/// Port of <c>packages/astro/test/asset-query-params.test.ts</c>: an adapter can declare <c>client.assetQueryParams</c>, and
/// Astro then appends that query string (a deployment id such as <c>dpl=test-deploy-id</c>) to the URLs of the assets that a page
/// links and to the imports between client chunks. Upstream renders pages with <c>app.render()</c> of a test adapter that sets
/// the option; astro-adapter-jint has no such option, so the <c>cb-assets</c> fixture wraps the integration of the adapter
/// (<c>support/cb/asset-query-params.mjs</c>, switched on by the <c>cb.assetQueryParams</c> flag of the variant) and the pages
/// come from HTTP requests to the host.
/// <para>
/// Differences from upstream: the fixtures are the consolidated <c>cb-assets</c> (its on-demand pages under <c>/ssr/</c>
/// replace the pages of <c>astro-assets</c>, <c>multiple-jsx-renderers</c>, <c>astro-assets-prefix</c> and
/// <c>asset-query-params-chunks</c>; the island of the "Islands" test is the React counter, not a custom woof/meow JSX
/// renderer). Every URL a page gets is also requested from the host (200, the content type of the file, the immutable cache
/// header for <c>/_astro/</c>); the host maps a file by its path and ignores the query string. The two inter-chunk describes
/// of the upstream file are identical (one of them sets <c>outDir</c>), so one test covers both. In the inter-chunk test every
/// import found in a client script is also requested the way a browser resolves it.
/// </para>
/// </summary>
[Trait("Upstream", "core/asset-query-params.test.ts")]
public class AssetQueryParamsTests(SiteHost sites)
{
    private const string Fixture = "cb-assets";
    private const string Query = "?dpl=test-deploy-id";

    private static readonly object QueryParamsConfig = new { cb = new { assetQueryParams = new { dpl = "test-deploy-id" } } };

    private static readonly object QueryParamsWithMultiCdnConfig = new
    {
        cb = new { assetQueryParams = new { dpl = "test-deploy-id" } },
        @base = "/custom-base",
        build = new { assetsPrefix = new { js = "https://js.example.com", css = "https://css.example.com", fallback = "https://example.com" } },
    };

    [Fact(DisplayName = "Asset Query Parameters (Adapter Client Config): appends assetQueryParams to stylesheet URLs in SSR")]
    public async Task StylesheetUrlsGetTheQuery()
    {
        var site = await sites.GetAsync(Fixture, QueryParamsConfig);
        var html = await PageAssets.PageAsync(site, "/ssr/");

        var stylesheets = html.QuerySelectorAll("link[rel=stylesheet]");

        Assert.NotEmpty(stylesheets);
        foreach (var stylesheet in stylesheets)
        {
            var href = stylesheet.GetAttribute("href")!;

            Assert.Contains(Query, href);
            await PageAssets.AssertServedAsync(site, href);
        }
    }

    [Fact(DisplayName = "Asset Query Parameters (Adapter Client Config): appends assetQueryParams to Image component src")]
    public async Task ImageSrcGetsTheQuery()
    {
        var site = await sites.GetAsync(Fixture, QueryParamsConfig);
        var html = await PageAssets.PageAsync(site, "/ssr/");

        var src = html.QuerySelector("img#test-image")!.GetAttribute("src")!;

        Assert.Contains("dpl=test-deploy-id", src);
        await PageAssets.AssertServedAsync(site, src);
    }

    [Fact(DisplayName = "Asset Query Parameters with Islands: appends assetQueryParams to astro-island component and renderer URLs")]
    public async Task IslandUrlsGetTheQuery()
    {
        var site = await sites.GetAsync(Fixture, QueryParamsConfig);
        var html = await PageAssets.PageAsync(site, "/ssr/");

        var island = html.QuerySelector("astro-island")!;
        foreach (var attribute in new[] { "component-url", "renderer-url" })
        {
            var url = island.GetAttribute(attribute)!;

            Assert.Contains(Query, url);
            await PageAssets.AssertServedAsync(site, url);
        }
    }

    [Fact(DisplayName = "Asset Query Parameters in Inter-Chunk JS Imports: appends assetQueryParams to relative imports inside client JS chunks")]
    public async Task InterChunkImportsGetTheQuery()
    {
        var site = await sites.GetAsync(Fixture, QueryParamsConfig);
        var html = await PageAssets.PageAsync(site, "/ssr/chunks/");

        Assert.NotEmpty(html.QuerySelectorAll("script[src]"));

        var foundStaticImport = false;
        var foundDynamicImport = false;
        foreach (var (path, code) in ClientScripts.Read(site))
        {
            foreach (var import in ClientScripts.StaticImport.Matches(code).Select(match => match.Groups[1].Value))
            {
                foundStaticImport = true;
                Assert.True(import.Contains(Query), $"Static inter-chunk import should include assetQueryParams: {import} in {path}");
                await ClientScripts.AssertImportResolvesAsync(site, path, import);
            }

            foreach (var import in ClientScripts.DynamicImport.Matches(code).Select(match => match.Groups[1].Value))
            {
                foundDynamicImport = true;
                Assert.True(import.Contains(Query), $"Dynamic inter-chunk import should include assetQueryParams: {import} in {path}");
                await ClientScripts.AssertImportResolvesAsync(site, path, import);
            }
        }

        Assert.True(foundStaticImport, "Expected at least one static relative inter-chunk import in client JS files");
        Assert.True(foundDynamicImport, "Expected at least one dynamic relative inter-chunk import in client JS files");
    }

    [Fact(DisplayName = "Asset Query Parameters with Islands and assetsPrefix map: uses js assetsPrefix for island URLs while appending assetQueryParams")]
    public async Task IslandUrlsUseTheJsPrefixAndGetTheQuery()
    {
        var site = await sites.GetAsync(Fixture, QueryParamsWithMultiCdnConfig);
        var html = await PageAssets.PageAsync(site, "/custom-base/ssr/");

        var island = html.QuerySelector("astro-island")!;
        foreach (var attribute in new[] { "component-url", "renderer-url" })
        {
            var url = island.GetAttribute(attribute)!;

            Assert.Matches(@"^https://js\.example\.com/_astro/.*\?dpl=test-deploy-id$", url);
            await PageAssets.AssertServedAsync(site, url, "/custom-base");
        }
    }
}
