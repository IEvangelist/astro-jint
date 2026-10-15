namespace AstroJint.Conformance.Core;

/// <summary>
/// The <c>core-endpoints</c> fixture and the config variant that the tests of endpoint-runtime, endpoint-response, endpoint-routing,
/// api-routes, non-html-pages and astro-public share (the variant is also the name of the build output, so every class has to pass the
/// same one).
/// </summary>
internal static class EndpointsFixture
{
    public const string Name = "core-endpoints";

    /// <summary>
    /// The routes that the upstream static builds (api-routes, non-html-pages, astro-public) write as files, marked as prerendered. The
    /// fixture is <c>output: 'server'</c> otherwise, because the endpoint-routing tests need on-demand routes and a build of the
    /// whole fixture with <c>output: 'static'</c> would fail on <c>incorrect.ts</c>, which prerendering runs. The list replaces the one of
    /// the fixture config, so it has the route of <c>placeholder.png.ts</c> too, which is always prerendered.
    /// </summary>
    public static readonly object Prerendered = new
    {
        integrations = new object[]
        {
            Prerender("binary.dat.ts"),
            Prerender("about.json.ts"),
            Prerender("placeholder.png.ts"),
            Prerender("index.astro"),
            Prerender("external-files.astro"),
            Prerender("robots.txt.ts"),
        },
    };

    private static object Prerender(string componentEndsWith) => new { conformanceIntegration = "prerender-route", componentEndsWith };
}
