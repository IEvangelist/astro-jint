namespace AstroJint.Conformance.CoreBuild;

/// <summary>
/// Port of <c>packages/astro/test/css-pure-chunk-query-params.test.ts</c>: a Vue island loads two async components that import
/// the same stylesheet, which makes Rolldown emit a chunk that only holds CSS. Astro deletes such chunks, and it must not leave
/// an import of one behind, while it appends the query string of <c>client.assetQueryParams</c> to the imports that survive.
/// Upstream builds the fixture with <c>output: 'server'</c> and a test adapter that declares <c>assetQueryParams</c> (a
/// <c>dpl</c> deployment id) and reads the chunks from <c>dist/client</c>. Here the page of <c>cb-container</c> is built with the
/// jint adapter and the same query (the support code that adds it to the adapter is in <c>tests/conformance/support/cb</c>).
/// <para>
/// Differences from upstream: besides reading the imports out of the chunks, the first test requests every one of them from the
/// host the way a browser resolves it (against the importing script, query string included), so a chunk that is imported but
/// missing in <c>dist/client</c> is a failed request, and each import has to be served as JavaScript. Side-effect imports
/// (<c>import "./chunk.js"</c>) are included as upstream does.
/// </para>
/// </summary>
[Trait("Upstream", "core/css-pure-chunk-query-params.test.ts")]
public class CssPureChunkQueryParamsTests(SiteHost sites)
{
    private const string Fixture = "cb-container";

    private const string Query = "?dpl=test-deploy-id";

    private static readonly object QueryParamsConfig = new { cb = new { assetQueryParams = new { dpl = "test-deploy-id" } } };

    private static IEnumerable<string> RelativeImports(string code) =>
        ClientScripts.StaticImport.Matches(code).Concat(ClientScripts.DynamicImport.Matches(code)).Select(match => match.Groups[1].Value);

    [Fact(DisplayName = "does not leave dangling imports to deleted pure CSS chunks")]
    public async Task DoesNotLeaveDanglingImportsToDeletedPureCssChunks()
    {
        var site = await sites.GetAsync(Fixture, QueryParamsConfig);

        var scripts = ClientScripts.Read(site).ToList();
        Assert.True(scripts.Count > 0, "Should have at least one client JS file");

        foreach (var (path, code) in scripts)
        {
            foreach (var import in RelativeImports(code))
            {
                await ClientScripts.AssertImportResolvesAsync(site, path, import);
            }
        }
    }

    [Fact(DisplayName = "appends assetQueryParams to surviving JS imports")]
    public async Task AppendsAssetQueryParamsToSurvivingJsImports()
    {
        var site = await sites.GetAsync(Fixture, QueryParamsConfig);

        var foundImportWithParams = false;
        foreach (var (path, code) in ClientScripts.Read(site))
        {
            foreach (var import in RelativeImports(code).Where(import => import.Contains('?')))
            {
                foundImportWithParams = true;
                Assert.True(import.Contains(Query), $"Import should include assetQueryParams: {import} in {path}");
            }
        }

        Assert.True(foundImportWithParams, "Should have at least one JS import with query params");
    }
}
