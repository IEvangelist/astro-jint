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

    /// <summary>The cache provider reports whether its <c>onRequest</c> is given a <c>waitUntil</c> function.</summary>
    public static readonly object WaitUntilProbe = new { cache = new { provider = new { config = new { waitUntilProbe = true } } } };

    /// <summary>The image service <c>src/image-service.ts</c>, which does not use Sharp: its transform puts a marker in front of the bytes.</summary>
    public static readonly object CustomImageService = new { image = new { service = new { entrypoint = "./src/image-service.ts" } } };

    /// <summary>The image service <c>src/sharp-image-service.ts</c>, which is Astro's Sharp service with its own markup.</summary>
    public static readonly object SharpImageService = new { image = new { service = new { entrypoint = "./src/sharp-image-service.ts" } } };

    /// <summary>The image service <c>src/image-service.ts</c>, set by an integration instead of by <c>image.service</c>.</summary>
    public static readonly object ImageServiceFromIntegration = new
    {
        integrations = new[] { new { conformanceIntegration = "cf-image-service", entrypoint = "./src/image-service.ts" } },
    };
}
