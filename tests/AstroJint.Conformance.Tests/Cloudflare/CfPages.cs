namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// The shared fixture <c>cf-pages</c> and the changes to its Astro config that more than one test class asks for. Each
/// change is one object, so the classes that use it share its build.
/// </summary>
internal static class CfPages
{
    public const string Fixture = "cf-pages";

    /// <summary>Base <c>/blog/</c> and the redirects of the upstream fixture <c>with-base</c>.</summary>
    public static readonly object Base = new
    {
        @base = "/blog/",
        redirects = new Dictionary<string, string> { ["/a/redirect"] = "/", ["/a/redirect/"] = "/" },
    };

    /// <summary>A fully static site, as the upstream fixtures of the redirect tests are.</summary>
    public static readonly object Static = new { output = "static" };

    /// <summary>Assets in <c>/_custom/</c> instead of <c>/_astro/</c>.</summary>
    public static readonly object CustomAssets = new { build = new { assets = "_custom" } };
}
