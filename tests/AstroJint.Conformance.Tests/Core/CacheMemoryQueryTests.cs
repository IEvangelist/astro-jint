using System.Net;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/cache-memory-query.test.ts</c>. Both fixtures of the upstream test (<c>cache-memory-query</c> and
/// <c>cache-memory-query-include</c>) are one fixture here, <c>core-cache-memory-query</c>, whose config picks the memory cache provider
/// with the default query handling or with <c>query.include</c> set to <c>['page', 'sort']</c> (the latter through the
/// <c>conformance.queryInclude</c> switch of the config, because <c>memoryCache()</c> is not JSON). Upstream requests
/// <c>http://example.com/cached?...</c> through <c>app.render</c> and reads the <c>X-Astro-Cache</c> header of each response; the tests
/// send the same paths through the host. The memory cache lives in the engine, so every site here runs with a pool of one engine
/// (<c>PoolSize = 1</c>): the entries of the first request are then still there for the next one, which is what upstream's single in-process app
/// provides.
/// </summary>
[Trait("Upstream", "core/cache-memory-query.test.ts")]
public class CacheMemoryQueryTests(SiteHost sites)
{
    private const string Fixture = "core-cache-memory-query";

    private static readonly object IncludeConfig = new { conformance = new { queryInclude = new[] { "page", "sort" } } };

    private Task<ConformanceSite> DefaultSiteAsync() =>
        sites.GetAsync(Fixture, variant: "one-engine", configure: options => options.PoolSize = 1);

    private Task<ConformanceSite> IncludeSiteAsync() =>
        sites.GetAsync(Fixture, IncludeConfig, variant: "one-engine", configure: options => options.PoolSize = 1);

    private static async Task<string> CacheStatusAsync(ConformanceSite site, string path)
    {
        using var response = await site.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return string.Join(", ", response.HeaderValues("X-Astro-Cache"));
    }

    [Fact(DisplayName = "Memory cache provider \u2014 default query exclusions: excludes utm_* params from cache key by default")]
    public async Task DefaultQueryExclusions_UtmParamsAreNotPartOfTheCacheKey()
    {
        var site = await DefaultSiteAsync();

        Assert.Equal("MISS", await CacheStatusAsync(site, "/cached?page=1&utm_source=google"));
        // Same page, different UTM: a HIT
        Assert.Equal("HIT", await CacheStatusAsync(site, "/cached?page=1&utm_source=twitter&utm_medium=social"));
        // Same page, no UTM: also a HIT
        Assert.Equal("HIT", await CacheStatusAsync(site, "/cached?page=1"));
    }

    [Fact(DisplayName = "Memory cache provider \u2014 default query exclusions: excludes fbclid from cache key by default")]
    public async Task DefaultQueryExclusions_FbclidIsNotPartOfTheCacheKey()
    {
        var site = await DefaultSiteAsync();

        Assert.Equal("MISS", await CacheStatusAsync(site, "/cached?page=2"));
        Assert.Equal("HIT", await CacheStatusAsync(site, "/cached?page=2&fbclid=abc123"));
    }

    [Fact(DisplayName = "Memory cache provider \u2014 default query exclusions: excludes gclid from cache key by default")]
    public async Task DefaultQueryExclusions_GclidIsNotPartOfTheCacheKey()
    {
        var site = await DefaultSiteAsync();

        Assert.Equal("MISS", await CacheStatusAsync(site, "/cached?page=3"));
        Assert.Equal("HIT", await CacheStatusAsync(site, "/cached?page=3&gclid=xyz789"));
    }

    [Fact(DisplayName = "Memory cache provider \u2014 default query exclusions: still differentiates on non-excluded params")]
    public async Task DefaultQueryExclusions_OtherParamsStillMakeSeparateEntries()
    {
        var site = await DefaultSiteAsync();

        Assert.Equal("MISS", await CacheStatusAsync(site, "/cached?page=10"));
        Assert.Equal("MISS", await CacheStatusAsync(site, "/cached?page=20"));
    }

    [Fact(DisplayName = "Memory cache provider \u2014 query include: only includes allowlisted params in cache key")]
    public async Task QueryInclude_OnlyAllowlistedParamsAreInTheCacheKey()
    {
        var site = await IncludeSiteAsync();

        Assert.Equal("MISS", await CacheStatusAsync(site, "/cached?page=1&sort=name"));
        // Same allowed params + extra unknown param: a HIT
        Assert.Equal("HIT", await CacheStatusAsync(site, "/cached?page=1&sort=name&tracking=abc"));
        // Same allowed params, different order: also a HIT
        Assert.Equal("HIT", await CacheStatusAsync(site, "/cached?sort=name&page=1"));
    }

    [Fact(DisplayName = "Memory cache provider \u2014 query include: different allowed param values produce different entries")]
    public async Task QueryInclude_DifferentAllowedValuesMakeSeparateEntries()
    {
        var site = await IncludeSiteAsync();

        Assert.Equal("MISS", await CacheStatusAsync(site, "/cached?page=5&sort=name"));
        Assert.Equal("MISS", await CacheStatusAsync(site, "/cached?page=5&sort=price"));
    }

    [Fact(DisplayName = "Memory cache provider \u2014 query include: request with no query params matches request with only non-allowed params")]
    public async Task QueryInclude_NoQueryMatchesOnlyNonAllowedParams()
    {
        var site = await IncludeSiteAsync();

        Assert.Equal("MISS", await CacheStatusAsync(site, "/cached"));
        // Only non-allowed params: effectively the same key as no params
        Assert.Equal("HIT", await CacheStatusAsync(site, "/cached?utm_source=google&tracking=abc"));
    }
}
