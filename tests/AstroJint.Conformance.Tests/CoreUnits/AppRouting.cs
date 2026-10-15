namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// The <c>app-routing</c> fixture, for the routing tests of <c>packages/astro/test/units/routing</c> that go through
/// <c>App.render</c>. It has no pages of its own: each variant lists the routes it needs in its <c>routes</c> config (see
/// <c>astro.config.mjs</c>), which point at the components of <c>src/routes</c>, and the names of the middleware of
/// <c>src/middleware-library</c> it needs in <c>middleware</c>.
/// </summary>
internal static class AppRouting
{
    public const string Fixture = "app-routing";

    /// <summary>A route to inject; <paramref name="component"/> is the name of a file in <c>src/routes</c>, with its extension.</summary>
    public static object Route(string pattern, string component, bool prerender = false) =>
        new { pattern, entrypoint = $"./src/routes/{component}", prerender };

    /// <summary>
    /// A route whose component takes its content from <paramref name="data"/> (<c>html.astro</c>, <c>rewrite.astro</c>, ...): the
    /// config puts it in <c>__ROUTE_DATA__</c>, under the pattern of the route.
    /// </summary>
    public static object DataRoute(string pattern, string component, object data, bool prerender = false) =>
        new { pattern, entrypoint = $"./src/routes/{component}", prerender, data };

    /// <summary>The text of the first element that matches, as cheerio's <c>text()</c> gives it (empty when there is none).</summary>
    public static string Text(RawResponse response, string selector) =>
        ConformanceSite.ParseHtml(response.Text).QuerySelector(selector)?.TextContent ?? "";
}
