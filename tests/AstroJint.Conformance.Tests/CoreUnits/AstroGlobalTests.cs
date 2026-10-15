using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/render/astro-global.test.ts</c>: the <c>Astro</c> global of a page and of its components
/// (<c>Astro.url</c>, <c>Astro.site</c>, <c>Astro.routePattern</c>, <c>Astro.isPrerendered</c>, <c>Astro.slots</c> after
/// <c>Object.freeze(Astro)</c>), rendered by the compiled pages of the <c>app-render</c> fixture inside Jint.
/// <para>
/// Differences from upstream: the app options become two builds of the fixture. The default one has no <c>site</c> and no
/// <c>base</c>. The other has <c>site: 'https://mysite.dev/subsite/'</c> and <c>base: '/blog'</c>; the host answers only requests
/// under the base, where upstream's mock app took <c>/</c> with <c>base: '/blog'</c> set, so those tests request
/// <c>/blog/…</c> and 'Astro.url propagates through parent, child, and nested-child components' sees the pathname with the base
/// (<c>/blog/</c>) where the upstream request had none. The tests that upstream ran with only a <c>site</c>
/// ('Astro.site', 'Astro.isPrerendered is false in SSR mode') and with <c>base: '/new'</c> (the 'SSR app' group) run on that
/// build too: the property they check does not depend on the base. The pages are on-demand (the fixture is
/// <c>output: 'server'</c>), so the <c>serverLike</c> option of the two <c>isPrerendered</c> tests is the build. The middleware
/// of the fixture stashes <c>routePattern</c> and <c>isPrerendered</c> in the locals, like upstream's, and every page shows both
/// values.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/render/astro-global.test.ts")]
public class AstroGlobalTests(SiteHost sites)
{
    // #region with site and base config

    [Fact(DisplayName = "with site and base config: Astro.url propagates through parent, child, and nested-child components")]
    public async Task AstroUrl_PropagatesThroughTheComponents()
    {
        var site = await sites.GetSiteAndBaseSiteAsync();

        var response = await site.SendRawAsync("/blog/");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("/blog/", AppRouting.Text(response, "#pathname"));
        Assert.Equal("{}", AppRouting.Text(response, "#searchparams"));
        Assert.Equal("/blog/", AppRouting.Text(response, "#child-pathname"));
        Assert.Equal("/blog/", AppRouting.Text(response, "#nested-child-pathname"));
    }

    [Fact(DisplayName = "with site and base config: Astro.site")]
    public async Task AstroSite_IsTheConfiguredSite()
    {
        var site = await sites.GetSiteAndBaseSiteAsync();

        var response = await site.SendRawAsync("/blog/");

        Assert.Equal("https://mysite.dev/subsite/", Html(response).QuerySelector("#site")?.GetAttribute("href"));
    }

    [Fact(DisplayName = "Astro.routePattern has the right value for static routes")]
    public async Task RoutePattern_OfStaticRoutes_IsTheRoute()
    {
        var site = await sites.GetSiteAndBaseSiteAsync();

        var index = await site.SendRawAsync("/blog/");
        Assert.Matches(@"Astro route pattern: /", AppRouting.Text(index, "#pattern"));
        Assert.Matches(@"Astro route pattern middleware: /", AppRouting.Text(index, "#pattern-middleware"));

        var named = await site.SendRawAsync("/blog/omit-markdown-extensions");
        Assert.Matches(@"Astro route pattern: /omit-markdown-extensions", AppRouting.Text(named, "#pattern"));
        Assert.Matches(
            @"Astro route pattern middleware: /omit-markdown-extensions", AppRouting.Text(named, "#pattern-middleware"));
    }

    [Fact(DisplayName = "Astro.routePattern has the right value for dynamic routes")]
    public async Task RoutePattern_OfDynamicRoutes_IsTheRoute()
    {
        var site = await sites.GetSiteAndBaseSiteAsync();

        var response = await site.SendRawAsync("/blog/posts/1");

        Assert.Equal("Astro route pattern: /posts/[page]", AppRouting.Text(response, "#pattern"));
        Assert.Equal("Astro route pattern middleware: /posts/[page]", AppRouting.Text(response, "#pattern-middleware"));
    }

    [Fact(DisplayName = "Astro.isPrerendered is false in SSR mode")]
    public Task IsPrerendered_InServerMode_IsFalse() => AssertNotPrerenderedAsync();

    // #endregion

    // #region SSR app

    [Fact(DisplayName = "SSR app: Astro.site")]
    public async Task SsrApp_AstroSite_IsTheConfiguredSite()
    {
        var site = await sites.GetSiteAndBaseSiteAsync();

        var response = await site.SendRawAsync("/blog/");

        Assert.Equal("https://mysite.dev/subsite/", Html(response).QuerySelector("#site")?.GetAttribute("href"));
    }

    [Fact(DisplayName = "Astro.routePattern has the right value in pages and components")]
    public async Task RoutePattern_InPagesAndComponents_IsTheRoute()
    {
        var site = await sites.GetSiteAndBaseSiteAsync();

        var index = await site.SendRawAsync("/blog/");
        Assert.Matches(@"Astro route pattern: /", AppRouting.Text(index, "#pattern"));
        Assert.Matches(@"Astro route pattern middleware: /", AppRouting.Text(index, "#pattern-middleware"));

        var named = await site.SendRawAsync("/blog/omit-markdown-extensions");
        Assert.Matches(@"Astro route pattern: /omit-markdown-extensions", AppRouting.Text(named, "#pattern"));
        Assert.Matches(
            @"Astro route pattern middleware: /omit-markdown-extensions", AppRouting.Text(named, "#pattern-middleware"));
    }

    [Fact(DisplayName = "Astro.isPrerendered is false with adapter and output server")]
    public Task IsPrerendered_WithTheAdapterAndOutputServer_IsFalse() => AssertNotPrerenderedAsync();

    private async Task AssertNotPrerenderedAsync()
    {
        var site = await sites.GetSiteAndBaseSiteAsync();

        foreach (var path in new[] { "/blog/", "/blog/about" })
        {
            var response = await site.SendRawAsync(path);

            Assert.Equal((path, "Astro route prerender: false"), (path, AppRouting.Text(response, "#prerender")));
            Assert.Equal(
                (path, "Astro route prerender middleware: false"), (path, AppRouting.Text(response, "#prerender-middleware")));
        }
    }

    // #endregion

    // #region Astro Global Defaults

    [Fact(DisplayName = "can access slots after the Astro global is frozen")]
    public async Task Slots_AfterTheAstroGlobalIsFrozen_AreAvailable()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/frozen");

        Assert.Contains("<p>true</p>", response.Text);
    }

    [Fact(DisplayName = "Astro.request.url with no site or base")]
    public async Task AstroUrl_WithNoSiteOrBase_IsTheRequestUrl()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/");

        Assert.Equal("/", AppRouting.Text(response, "#pathname"));
        Assert.Equal("{}", AppRouting.Text(response, "#searchparams"));
        Assert.Equal("/", AppRouting.Text(response, "#child-pathname"));
        Assert.Equal("/", AppRouting.Text(response, "#nested-child-pathname"));
    }

    [Fact(DisplayName = "Astro.site is undefined when no site configured")]
    public async Task AstroSite_WithoutASite_IsUndefined()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/");

        Assert.Null(Html(response).QuerySelector("#site")?.GetAttribute("href"));
    }

    // #endregion
}
