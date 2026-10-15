using System.Net;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/astro-public.test.ts</c> on the <c>astro-public</c> fixture, whose <c>public</c> folder and pages are part of
/// <c>core-endpoints</c> (see <see cref="EndpointsFixture"/>).
/// <list type="bullet">
/// <item>The four tests of <c>Public</c> build the fixture with no adapter and read <c>/index.html</c>, <c>/robots.txt</c> and
/// <c>/external-files/index.html</c> from the output. The build here is the one of the fixture with its routes prerendered
/// (<see cref="EndpointsFixture.Prerendered"/>), the files are in <c>dist/client</c> and the tests read them from there, so they are
/// ported. The pages and the public files are requested from the host too (not asserted upstream).</item>
/// <item>The upstream test of the empty chunk warning collects the messages of an <c>AstroLogger</c> that it hands to the build, with the log
/// level of Vite at <c>info</c>. The port has the whole output of <c>astro build</c>, which is what that logger prints, and looks in its lines;
/// it checks that the log has the warnings of the build as well, since the absence of one would mean nothing in a log that has none.</item>
/// <item><c>Public (dev)</c>: <c>GET /robots.txt</c> on the dev server. The port requests the file from the host with the fixture built as
/// <c>output: 'server'</c>, where <c>robots.txt.ts</c> is an on-demand route that the public file has to win over, as the static
/// files of the client output win over the routes of the engine: adapted.</item>
/// </list>
/// <para>Not upstream: the on-demand page keeps its references to the public files (they are not processed there either), and the host
/// serves the files of the <c>public</c> folder with their content types.</para>
/// </summary>
[Trait("Upstream", "core/astro-public.test.ts")]
public class AstroPublicTests(SiteHost sites)
{
    private static void AssertPageReferencesThePublicFiles(string html)
    {
        Assert.Contains("""<script src="/example.js"></script>""", html);
        Assert.Contains("""<link href="/example.css" rel="stylesheet">""", html);
        Assert.Contains("""<img src="/images/twitter.png">""", html);
    }

    private static void AssertIsThePublicRobotsTxt(string robotsTxt)
    {
        // Should contain public file content
        Assert.Matches(@"Disallow: /admin/", robotsTxt);
        // Should not contain API route content
        Assert.DoesNotMatch(@"Disallow: /\n", robotsTxt);
    }

    [Fact(DisplayName = "Public: css and js files do not get bundled")]
    public async Task Build_CssAndJsFilesOfThePublicFolder_AreNotBundled()
    {
        var site = await sites.GetAsync(EndpointsFixture.Name, EndpointsFixture.Prerendered);

        var indexHtml = BuildOutput.ReadClientText(site, "index.html");

        AssertPageReferencesThePublicFiles(indexHtml);
        // Not asserted upstream: the host serves the page that the build wrote.
        using var response = await site.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(indexHtml, await response.ReadTextAsync());
    }

    [Fact(DisplayName = "Public: should not produce empty chunk warning when building with no client JS")]
    public async Task Build_WithNoClientJs_HasNoEmptyChunkWarning()
    {
        var site = await sites.GetAsync(EndpointsFixture.Name, EndpointsFixture.Prerendered);

        var lines = site.BuildLog.Split('\n');

        // Not asserted upstream: the log has the warnings of the build.
        Assert.Contains(lines, line => line.Contains("[WARN]", StringComparison.Ordinal));
        // Check for empty chunk warnings in the build logs
        var emptyChunkWarning = lines.FirstOrDefault(line =>
            line.Contains("empty chunk", StringComparison.Ordinal)
            || (line.Contains("empty", StringComparison.Ordinal) && line.Contains("chunk", StringComparison.Ordinal)));
        Assert.True(emptyChunkWarning is null, $"Should not have empty chunk warning, but got: {emptyChunkWarning}");
    }

    [Fact(DisplayName = "Public: public file takes priority over API route in build")]
    public async Task Build_PublicFile_TakesPriorityOverTheApiRoute()
    {
        var site = await sites.GetAsync(EndpointsFixture.Name, EndpointsFixture.Prerendered);

        // When both public/robots.txt and src/pages/robots.txt.ts exist,
        // the public file should take priority
        var robotsTxt = BuildOutput.ReadClientText(site, "robots.txt");

        AssertIsThePublicRobotsTxt(robotsTxt);
        // Not asserted upstream: the host serves that file.
        using var response = await site.GetAsync("/robots.txt");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(robotsTxt, await response.ReadTextAsync());
    }

    [Fact(DisplayName = "Public: Build with external reference")]
    public async Task Build_PageWithAnExternalScript_KeepsItsReference()
    {
        var site = await sites.GetAsync(EndpointsFixture.Name, EndpointsFixture.Prerendered);

        var html = BuildOutput.ReadClientText(site, "external-files/index.html");

        Assert.Contains("<script src=\"/external-file.js\"", html);
        // Not asserted upstream: the host serves the page that the build wrote and the script that it references.
        using var page = await site.GetAsync("/external-files/");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal(html, await page.ReadTextAsync());
        using var script = await site.GetAsync("/external-file.js");
        Assert.Equal(HttpStatusCode.OK, script.StatusCode);
        Assert.Equal(BuildOutput.ReadClientText(site, "external-file.js"), await script.ReadTextAsync());
    }

    [Fact(DisplayName = "Public (dev): public file takes priority over API route in dev")]
    public async Task OnDemand_PublicFile_TakesPriorityOverTheApiRoute()
    {
        var site = await sites.GetAsync(EndpointsFixture.Name);

        // When both public/robots.txt and src/pages/robots.txt.ts exist,
        // the public file should take priority
        using var response = await site.GetAsync("/robots.txt");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertIsThePublicRobotsTxt(await response.ReadTextAsync());
    }

    [Fact(DisplayName = "Not upstream: Public: an on-demand page keeps its references to the public files")]
    public async Task OnDemand_PageReferencingPublicFiles_KeepsTheReferences()
    {
        var site = await sites.GetAsync(EndpointsFixture.Name);

        // The route is on demand in this build: it has the engine, not a file, to answer it.
        Assert.False(site.PathExists("client/index.html"));
        using var response = await site.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertPageReferencesThePublicFiles(await response.ReadTextAsync());
    }

    [Fact(DisplayName = "Not upstream: Public: the files of the public folder are served with their content types")]
    public async Task PublicFiles_AreServedAsTheyAre()
    {
        var site = await sites.GetAsync(EndpointsFixture.Name);

        foreach (var (path, contentType) in new[]
        {
            ("/example.css", "text/css"),
            ("/example.js", "text/javascript"),
            ("/external-file.js", "text/javascript"),
            ("/images/twitter.png", "image/png"),
        })
        {
            using var response = await site.GetAsync(path);

            Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path} answered {(int)response.StatusCode}");
            Assert.Equal(contentType, response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(BuildOutput.ReadClientBytes(site, path), await response.Content.ReadAsByteArrayAsync());
        }
    }
}
