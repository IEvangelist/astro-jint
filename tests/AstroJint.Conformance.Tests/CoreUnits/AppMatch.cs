using System.Text.Json;
using static AstroJint.Conformance.CoreUnits.AppRouting;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// What <c>match-info.astro</c> or <c>match-endpoint.ts</c> of the <c>app-routing</c> fixture answered: the pattern of the route
/// that rendered the request, the params it got as compact JSON (<c>{}</c> when there are none, a param that is undefined is
/// left out), the names of the keys of the params joined by commas (a param that is undefined is listed) and the pathname it saw.
/// </summary>
internal sealed record Matched(int Status, string Pattern, string Params, string Keys, string Pathname);

/// <summary>
/// The route tables of the router tests of <c>packages/astro/test/units/routing</c> (routing-priority, router-match, trailing-slash
/// and the others that build a <c>Router</c> from <c>makeRoute()</c> descriptions), as variants of <c>app-routing</c> that inject
/// the same patterns, with the pages and endpoints that report which route answered.
/// </summary>
internal static class AppMatch
{
    /// <summary>A route rendered by <c>match-info.astro</c>.</summary>
    public static object Page(string pattern) => Route(pattern, "match-info.astro");

    /// <summary>A route answered by <c>match-endpoint.ts</c>.</summary>
    public static object Endpoint(string pattern) => Route(pattern, "match-endpoint.ts");

    // routing-priority.test.ts (and the injected route of virtual-routes.test.ts).
    public static readonly object Priority = new
    {
        routes = new[]
        {
            Page("/"),
            Page("/de"),
            Page("/[lang]"),
            Page("/[lang]/[...catchall]"),
            Page("/posts/[pid]"),
            Page("/posts/[...slug]"),
            Page("/injected"),
            Page("/_injected"),
            Page("/virtual"),
            Endpoint("/api/catch/[...slug].json"),
            Endpoint("/api/catch/[foo]-[bar].json"),
            Page("/empty-slug/[...slug]"),
        },
    };

    // dynamic-route-collision.test.ts and the first tests of router-match.test.ts, with trailingSlash 'ignore'.
    public static readonly object Collision = new
    {
        routes = new[]
        {
            Page("/[slug]"),
            Page("/about"),
            Page("/[...slug]"),
            Page("/test"),
            Page("/game-[title]"),
            Page("/posts/[id]"),
            Page("/posts/[...slug]"),
        },
    };

    // resolved-pathname.test.ts (the endpoints and their page twins), route-sanitization.test.ts (the spread) and the 'never' test of router-match.test.ts.
    public static readonly object NeverInTheFileFormat = new
    {
        trailingSlash = "never",
        build = new { format = "file" },
        routes = new[]
        {
            Endpoint("/api/[category]"),
            Endpoint("/api/[category]/[id]"),
            Page("/pages/[category]"),
            Page("/pages/[category]/[id]"),
            Page("/blog"),
            Page("/[...testSlashTrim]"),
        },
    };

    // The routes of the 'always' group of trailing-slash.test.ts and the route of the 'always' test of router-match.test.ts.
    public static readonly object Always = new
    {
        trailingSlash = "always",
        routes = new[]
        {
            Endpoint("/api"),
            Endpoint("/dot.json"),
            Endpoint("/pathname"),
            Endpoint("/subpage"),
            Endpoint("/injected"),
            Endpoint("/injected.json"),
            Page("/blog"),
        },
    };

    // The 'never' group of trailing-slash.test.ts with base '/base'.
    public static readonly object NeverWithBase = new
    {
        trailingSlash = "never",
        @base = "/base",
        routes = new[]
        {
            Endpoint("/api"),
            Endpoint("/dot.json"),
            Endpoint("/pathname"),
            Endpoint("/subpage"),
            Endpoint("/"),
            Endpoint("/injected"),
        },
    };

    // The 'never' group of trailing-slash.test.ts with base '/mybase' (issue #13736).
    public static readonly object NeverWithMyBase = new
    {
        trailingSlash = "never",
        @base = "/mybase",
        routes = new[]
        {
            Endpoint("/api"),
            Endpoint("/dot.json"),
            Endpoint("/pathname"),
            Endpoint("/subpage"),
            Endpoint("/"),
        },
    };

    // The 'redirects base path when trailingSlash=always' test of router-match.test.ts.
    public static readonly object AlwaysWithBase = new
    {
        trailingSlash = "always",
        @base = "/blog",
        routes = new[] { Page("/") },
    };

    // The .html tests of router-match.test.ts for build.format 'file', with a base.
    public static readonly object FileFormatWithBase = new
    {
        @base = "/blog",
        build = new { format = "file" },
        routes = new[] { Page("/"), Page("/about"), Page("/[slug].html") },
    };

    // The .html tests of router-match.test.ts for build.format 'directory'.
    public static readonly object HtmlInRoutes = new
    {
        routes = new[] { Page("/[slug].html"), Endpoint("/files/[path]") },
    };

    // dev-routing.test.ts, and the leading slashes tests of router-match.test.ts. The patterns are \u30C6\u30B9\u30C8 and "te st".
    public static readonly object DevRouting = new
    {
        routes = new[]
        {
            Page("/"),
            Page("/\u30C6\u30B9\u30C8"),
            Page("/te st"),
            Page("/another"),
            Page("/[id]"),
            Page("/foo/bar"),
        },
    };

    /// <summary>Asks for <paramref name="target"/> and reads what the answer of a route of this fixture says.</summary>
    public static async Task<Matched> RequestMatchAsync(this ConformanceSite site, string target) =>
        Read(await site.SendRawAsync(target));

    public static Matched Read(RawResponse response)
    {
        var contentType = response.Header("Content-Type") ?? "";
        if (response.StatusCode != 200)
        {
            return new Matched(response.StatusCode, "", "", "", "");
        }

        if (contentType.StartsWith("application/json", StringComparison.Ordinal))
        {
            using var json = JsonDocument.Parse(response.Text);
            var root = json.RootElement;
            return new Matched(
                response.StatusCode,
                root.GetProperty("pattern").GetString() ?? "",
                root.GetProperty("params").GetRawText(),
                string.Join(',', root.GetProperty("keys").EnumerateArray().Select(key => key.GetString())),
                root.GetProperty("pathname").GetString() ?? "");
        }

        return new Matched(
            response.StatusCode,
            Text(response, "#pattern"),
            Text(response, "#params"),
            Text(response, "#keys"),
            Text(response, "#pathname"));
    }
}
