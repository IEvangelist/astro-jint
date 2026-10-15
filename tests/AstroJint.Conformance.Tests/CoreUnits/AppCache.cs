namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// The three builds of the <c>app-cache</c> fixture and the sites that the cache tests use: the memory provider of Astro, a
/// provider that only sets headers (the CDN style, which has no <c>onRequest</c>) and no provider at all.
/// </summary>
internal static class AppCache
{
    public const string Fixture = "app-cache";

    // The memory provider keeps its entries in the module state of the engine that runs the request. The default host has a pool of
    // engines, and which of them takes a request decides whether it finds the entry of an earlier one, so the site has a single
    // engine. Its variant names a host that no other class shares.
    public static Task<ConformanceSite> GetMemoryCacheSiteAsync(this SiteHost sites) =>
        sites.GetAsync(
            Fixture, new { conformance = new { provider = "memory" } }, variant: "single-engine", configure: options => options.PoolSize = 1);

    public static Task<ConformanceSite> GetCdnCacheSiteAsync(this SiteHost sites) =>
        sites.GetAsync(Fixture, new { conformance = new { provider = "cdn" } });

    public static Task<ConformanceSite> GetDisabledCacheSiteAsync(this SiteHost sites) =>
        sites.GetAsync(Fixture);
}
