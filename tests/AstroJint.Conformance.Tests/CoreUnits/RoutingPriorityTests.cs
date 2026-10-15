using static AstroJint.Conformance.CoreUnits.AppRouting;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/routing/routing-priority.test.ts</c>: which of several routes that could match a path
/// answers it (static before dynamic before spread, per segment) and which params it gets.
/// <para>
/// Differences from upstream: the test builds a <c>Router</c> from <c>makeRoute()</c> descriptions and reads <c>match.route.component</c>
/// and <c>match.params</c>. Here the same patterns are injected as routes of a real build (<see cref="AppMatch.Priority"/>) and each
/// request is answered by <c>match-info.astro</c> or <c>match-endpoint.ts</c>, which report <c>Astro.routePattern</c> and the params.
/// The pattern stands for the component (<c>[lang]/index.astro</c> is <c>/[lang]</c>). 'matches empty spread params as undefined'
/// checks that the key is there with an undefined value (the page lists the keys; JSON leaves undefined out).
/// </para>
/// </summary>
[Trait("Upstream", "core-units/routing/routing-priority.test.ts")]
public class RoutingPriorityTests(SiteHost sites)
{
    private async Task<Matched> MatchAsync(string target)
    {
        var site = await sites.GetAsync(Fixture, AppMatch.Priority);
        var match = await site.RequestMatchAsync(target);
        Assert.Equal(200, match.Status);
        return match;
    }

    [Fact(DisplayName = "matches / to index route")]
    public async Task Root_MatchesTheIndexRoute()
    {
        var match = await MatchAsync("/");

        Assert.Equal("/", match.Pattern);
    }

    [Fact(DisplayName = "matches static locale route before dynamic")]
    public async Task StaticLocaleRoute_IsMatchedBeforeTheDynamicOne()
    {
        var match = await MatchAsync("/de");

        Assert.Equal("/de", match.Pattern);
        Assert.Equal("{}", match.Params);
    }

    [Fact(DisplayName = "matches dynamic locale route when static does not exist")]
    public async Task DynamicLocaleRoute_IsMatchedWhenThereIsNoStaticOne()
    {
        var match = await MatchAsync("/en");

        Assert.Equal("/[lang]", match.Pattern);
        Assert.Equal("""{"lang":"en"}""", match.Params);
    }

    [Fact(DisplayName = "matches dynamic catchall under locale")]
    public async Task DynamicCatchallUnderTheLocale_IsMatched()
    {
        var match = await MatchAsync("/de/1/2");

        Assert.Equal("/[lang]/[...catchall]", match.Pattern);
        Assert.Equal("""{"lang":"de","catchall":"1/2"}""", match.Params);
    }

    [Fact(DisplayName = "matches posts/[pid] over posts/[...slug]")]
    public async Task PostsWithASingleParam_IsMatchedOverTheSpread()
    {
        var match = await MatchAsync("/posts/post-1");

        Assert.Equal("/posts/[pid]", match.Pattern);
        Assert.Equal("""{"pid":"post-1"}""", match.Params);
    }

    [Fact(DisplayName = "matches posts/[...slug] for deeper paths")]
    public async Task PostsSpread_IsMatchedForDeeperPaths()
    {
        var match = await MatchAsync("/posts/1/2");

        Assert.Equal("/posts/[...slug]", match.Pattern);
        Assert.Equal("""{"slug":"1/2"}""", match.Params);
    }

    [Fact(DisplayName = "matches injected static routes")]
    public async Task InjectedStaticRoutes_AreMatched()
    {
        var injected = await MatchAsync("/injected");
        var underscored = await MatchAsync("/_injected");

        Assert.Equal("/injected", injected.Pattern);
        Assert.Equal("/_injected", underscored.Pattern);
    }

    [Fact(DisplayName = "matches endpoint catch-all JSON route")]
    public async Task EndpointCatchAllJsonRoute_IsMatched()
    {
        var match = await MatchAsync("/api/catch/a.json");

        Assert.Equal("/api/catch/[...slug].json", match.Pattern);
        Assert.Equal("""{"slug":"a"}""", match.Params);
    }

    [Fact(DisplayName = "matches endpoint catch-all JSON route for nested paths")]
    public async Task EndpointCatchAllJsonRoute_IsMatchedForNestedPaths()
    {
        var match = await MatchAsync("/api/catch/b/c.json");

        Assert.Equal("/api/catch/[...slug].json", match.Pattern);
        Assert.Equal("""{"slug":"b/c"}""", match.Params);
    }

    [Fact(DisplayName = "matches endpoint [foo]-[bar].json over catch-all")]
    public async Task EndpointWithTwoParams_IsMatchedOverTheCatchAll()
    {
        var match = await MatchAsync("/api/catch/a-b.json");

        Assert.Equal("/api/catch/[foo]-[bar].json", match.Pattern);
        Assert.Equal("""{"foo":"a","bar":"b"}""", match.Params);
    }

    [Fact(DisplayName = "matches empty spread params as undefined")]
    public async Task EmptySpreadParam_IsUndefined()
    {
        var match = await MatchAsync("/empty-slug");

        Assert.Equal("/empty-slug/[...slug]", match.Pattern);
        Assert.Equal("{}", match.Params);
        Assert.Equal("slug", match.Keys);
    }
}
