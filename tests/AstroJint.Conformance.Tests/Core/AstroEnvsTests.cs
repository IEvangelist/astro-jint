using System.Net;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/astro-envs.test.ts</c>. The fixture is <c>astro-envs</c> of Astro (<c>core-astro-envs</c>): a site with
/// <c>site: 'http://example.com'</c> and <c>base: '/blog'</c> that prints <c>import.meta.env</c> (public, private and built-in variables,
/// plain and destructured), a Vue component with <c>client:load</c>, and a JSON file that a Vite plugin of the config marks with the
/// <c>env</c> it saw. The site is requested from the host under its base, <c>/blog/</c>.
/// <list type="bullet">
/// <item>The <c>Build</c> tests set <c>process.env.BOOLEAN_VAR</c> and <c>NUMBER_VAR</c> in the test process before they build. The build
/// runs in another process here, so the config of the fixture sets the two variables itself (the <c>conformance.buildEnv</c> switch of
/// its config, as in the other fixtures of this folder). Upstream reads <c>index.html</c> and <c>destructured/index.html</c> from the output
/// directory; the pages are prerendered, so the tests read the same HTML from the host, which serves it from <c>dist/client</c>. The two
/// tests about client-side JS read the files of <c>dist/client</c>. <c>does not include private env in client-side JS</c> lists only the
/// root of the output directory upstream, where there are no scripts (the check cannot fail); here it reads every script of the client
/// build and requires that there are some. That is stricter than upstream.</item>
/// <item>The <c>Development</c> tests run a dev server upstream. The environment variables are inlined into the modules by the production
/// build in the same way, and the pages print them, so the tests request the production build and are adapted.</item>
/// <item>The <c>SSR</c> test builds the fixture with <c>output: 'server'</c> and <c>process.env.SECRET_PLACE</c> set to
/// <c>SECRET_PLACE_BUILD</c> (the config's <c>conformance.buildEnv</c> again), then changes the variable to <c>SECRET_PLACE_SSR</c> before it
/// renders. The host does that with <c>AstroOptions.Env</c>, which is what <c>process.env</c> reads in the engine, and it requests the page
/// under the base of the site (upstream renders <c>/</c>). Upstream asserts that the page holds the value of the build; the test also
/// asserts that it does not hold the one of the runtime, which is not asserted upstream.</item>
/// </list>
/// </summary>
[Trait("Upstream", "core/astro-envs.test.ts")]
public class AstroEnvsTests(SiteHost sites)
{
    private const string Fixture = "core-astro-envs";

    private static IEnumerable<string> ClientScripts(ConformanceSite site) =>
        Directory.EnumerateFiles(Path.Combine(site.DistPath, "client"), "*.js", SearchOption.AllDirectories);

    private static async Task<string> GetPageAsync(ConformanceSite site, string path)
    {
        using var response = await site.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadTextAsync();
    }

    [Fact(DisplayName = "Environment Variables: Build: builds without throwing")]
    public async Task Build_Succeeds()
    {
        var site = await sites.GetAsync(Fixture);

        Assert.True(site.PathExists("client/index.html"), "the build must write the prerendered page");
        Assert.True(site.PathExists("server/entry.mjs"), "the build must write the server entry");
    }

    [Fact(DisplayName = "Environment Variables: Build: does render public env and private env")]
    public async Task Index_RendersPublicAndPrivateEnv()
    {
        var site = await sites.GetAsync(Fixture);

        var html = await GetPageAsync(site, "/blog/");

        Assert.Contains("CLUB_33", html);
        Assert.Contains("BLUE_BAYOU", html);
    }

    [Fact(DisplayName = "Environment Variables: Build: does render destructured public env and private env")]
    public async Task Destructured_RendersPublicAndPrivateEnv()
    {
        var site = await sites.GetAsync(Fixture);

        var html = await GetPageAsync(site, "/blog/destructured/");

        Assert.Contains("CLUB_33", html);
        Assert.Contains("BLUE_BAYOU", html);
    }

    [Fact(DisplayName = "Environment Variables: Build: does render builtin SITE env")]
    public async Task Index_RendersTheBuiltinSiteEnv()
    {
        var site = await sites.GetAsync(Fixture);

        var html = await GetPageAsync(site, "/blog/");

        Assert.Contains("http://example.com", html);
    }

    [Fact(DisplayName = "Environment Variables: Build: does render destructured builtin SITE env")]
    public async Task Destructured_RendersTheBuiltinSiteEnv()
    {
        var site = await sites.GetAsync(Fixture);

        var html = await GetPageAsync(site, "/blog/destructured/");

        Assert.Contains("http://example.com", html);
    }

    [Fact(DisplayName = "Environment Variables: Build: does render builtin BASE_URL env")]
    public async Task Index_RendersTheBuiltinBaseUrlEnv()
    {
        var site = await sites.GetAsync(Fixture);

        var html = await GetPageAsync(site, "/blog/");

        Assert.Contains("/blog", html);
    }

    [Fact(DisplayName = "Environment Variables: Build: includes public env in client-side JS")]
    public async Task ClientScripts_IncludeThePublicEnv()
    {
        var site = await sites.GetAsync(Fixture);

        var found = ClientScripts(site).Any(script => File.ReadAllText(script).Contains("BLUE_BAYOU", StringComparison.Ordinal));

        Assert.True(found, "found the public env variable in the JS build");
    }

    [Fact(DisplayName = "Environment Variables: Build: does not include private env in client-side JS")]
    public async Task ClientScripts_DoNotIncludeThePrivateEnv()
    {
        var site = await sites.GetAsync(Fixture);

        var scripts = ClientScripts(site).ToList();
        var found = scripts.Any(script => File.ReadAllText(script).Contains("CLUB_33", StringComparison.Ordinal));

        Assert.NotEmpty(scripts);
        Assert.False(found, "found the private env variable in the JS build");
    }

    [Fact(DisplayName = "Environment Variables: Build: does not coerce environment variable values")]
    public async Task Index_DoesNotCoerceEnvironmentVariableValues()
    {
        var site = await sites.GetAsync(Fixture);

        var html = await GetPageAsync(site, "/blog/");

        Assert.Contains("typeof BOOLEAN_VAR is string", html);
        Assert.Contains("typeof NUMBER_VAR is string", html);
    }

    [Fact(DisplayName = "Environment Variables: Development: does render builtin BASE_URL env")]
    public async Task Index_BaseUrlElementHoldsTheBase()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/blog/");
        var html = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("/blog", html.QuerySelector("#base-url")!.TextContent);
    }

    [Fact(DisplayName = "Environment Variables: Development: does render destructured builtin SITE env")]
    public async Task Destructured_BaseUrlElementHoldsTheBase()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/blog/destructured/");
        var html = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("/blog", html.QuerySelector("#base-url")!.TextContent);
    }

    [Fact(DisplayName = "Environment Variables: Development: does not inject env into imported asset files")]
    public async Task ImportedAssets_DoNotGetTheEnvInjected()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/blog/");
        var html = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("A MYSTERY", html.QuerySelector("#env")!.TextContent);
        Assert.Equal("good", html.QuerySelector("#css")!.TextContent);
    }

    [Fact(DisplayName = "Environment Variables: SSR: does not turn import.meta.env into process.env")]
    public async Task Ssr_ImportMetaEnvIsNotTheRuntimeEnv()
    {
        var site = await sites.GetAsync(
            Fixture,
            new { output = "server", conformance = new { buildEnv = new { SECRET_PLACE = "SECRET_PLACE_BUILD" } } },
            variant: "secret-place-at-runtime",
            configure: options => options.Env["SECRET_PLACE"] = "SECRET_PLACE_SSR");

        var html = await GetPageAsync(site, "/blog/");

        Assert.Contains("SECRET_PLACE_BUILD", html);
        Assert.DoesNotContain("SECRET_PLACE_SSR", html);
    }
}
