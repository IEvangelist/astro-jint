using System.Net;
using AngleSharp.Dom;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/serializeManifest.test.ts</c>: the <c>astro:config/client</c> and <c>astro:config/server</c> virtual modules,
/// which expose the (serialized) config of the build to pages and client scripts. The test builds three fixtures; here two cover them:
/// <list type="bullet">
/// <item><c>core-astro-manifest</c> is <c>astro-manifest</c> (the config below it is that of upstream plus the Jint adapter; an integration sets
/// <c>base: '/blog'</c>, <c>site</c> and <c>trailingSlash: 'always'</c> in <c>astro:config:setup</c>, which is why the pages are served under
/// <c>/blog/</c>) with the page of <c>astro-manifest-client-script</c> added as <c>client-script.astro</c> (not an upstream path: merging the
/// two fixtures saves a build, and every build of the fixture builds that page too, so building it is the check of the client-script test).</item>
/// <item><c>core-astro-manifest-invalid</c> is <c>astro-manifest-invalid</c>.</item>
/// </list>
/// What upstream reads with <c>fixture.readFile</c> (the static build, no adapter) is requested over HTTP here, where the host serves the files
/// of <c>dist/client</c>: ported, not adapted. The SSR cases (<c>app.render</c> of <c>http://example.com/blog/server/</c>) are requests to the
/// host. The directory of the output is <c>dist</c> for a build with no config overrides and <c>dist-&lt;hash&gt;</c> for the others, so the
/// suffixes that upstream spells out (<c>/dist/serializeManifest-.../</c>) are built from the name of the directory of the build under test.
/// Upstream checks <c>error.errors[0]</c> to be an <c>AstroError</c> named <c>ServerOnlyModule</c>; the harness reports a failed build as an
/// exception with the output of <c>astro build</c>, where the CLI prints the name of the error, so the port looks for it there.
/// </summary>
[Trait("Upstream", "core/serializeManifest.test.ts")]
public class SerializeManifestTests(SiteHost sites)
{
    private const string Fixture = "core-astro-manifest";
    private const string InvalidFixture = "core-astro-manifest-invalid";

    private static readonly object SsrConfig = new { output = "server" };

    private static string TextOf(IDocument html, string id) => html.QuerySelector("#" + id)!.TextContent;

    private static async Task<IDocument> GetHtmlAsync(ConformanceSite site, string path)
    {
        using var response = await site.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadHtmlAsync();
    }

    private static async Task AssertServerConfigAsync(ConformanceSite site, string path)
    {
        var html = await GetHtmlAsync(site, path);
        var outDir = new DirectoryInfo(site.DistPath).Name;

        Assert.EndsWith($"/{outDir}/", TextOf(html, "out-dir"));
        Assert.EndsWith("/src/", TextOf(html, "src-dir"));
        Assert.EndsWith("/.astro/", TextOf(html, "cache-dir"));
        Assert.EndsWith("/", TextOf(html, "root"));
        Assert.EndsWith($"/{outDir}/client/", TextOf(html, "build-client"));
        Assert.EndsWith($"/{outDir}/server/", TextOf(html, "build-server"));
        Assert.Equal("https://cdn.example.com", TextOf(html, "build-assets-prefix"));
        // URL
        Assert.Equal("true", TextOf(html, "root-url"));
    }

    [Fact(DisplayName = "astro:config/client: when the experimental flag is enabled in build: should return the expected properties")]
    public async Task ClientConfig_StaticBuild_HasTheExpectedProperties()
    {
        var site = await sites.GetAsync(Fixture);

        var html = await GetHtmlAsync(site, "/blog/");

        Assert.Equal(
            """{"base":"/blog","i18n":{"defaultLocale":"en","locales":["en","fr"],"routing":{"prefixDefaultLocale":false,"redirectToDefaultLocale":true,"fallbackType":"redirect"}},"build":{"format":"directory"},"trailingSlash":"always","compressHTML":"jsx","site":"https://example.com"}""",
            TextOf(html, "config"));
    }

    [Fact(DisplayName = "astro:config/client in a client script: when build: should build without errors when astro:config/client is used in a client script")]
    public async Task ClientConfig_InAClientScript_BuildsWithoutErrors()
    {
        // A failing build throws from GetAsync, with the output of astro build.
        var site = await sites.GetAsync(Fixture, SsrConfig);

        // Not asserted upstream: the page of the client script is served and loads that script.
        var html = await GetHtmlAsync(site, "/blog/client-script/");
        Assert.NotEmpty(html.QuerySelectorAll("script[type=module]"));
    }

    [Fact(DisplayName = "astro:config/server: when build: should return an error when using inside a client script")]
    public async Task ServerConfig_InAClientScript_FailsTheBuildWithServerOnlyModule()
    {
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => sites.GetAsync(InvalidFixture));

        Assert.Contains("ServerOnlyModule", error.Message);
    }

    [Fact(DisplayName = "astro:config/server: when the experimental flag is enabled in build: should return the expected properties")]
    public async Task ServerConfig_StaticBuild_HasTheExpectedProperties()
    {
        var site = await sites.GetAsync(Fixture);

        await AssertServerConfigAsync(site, "/blog/server/");
    }

    [Fact(DisplayName = "astro:config/server: when the experimental flag is enabled in SSR: should return the expected properties")]
    public async Task ServerConfig_Ssr_HasTheExpectedProperties()
    {
        var site = await sites.GetAsync(Fixture, SsrConfig);

        await AssertServerConfigAsync(site, "/blog/server/");
    }
}
