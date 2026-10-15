using System.Net;
using System.Text;
using System.Web;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of the groups of <c>packages/astro/test/core-image.test.ts</c> that run an on-demand site: <c>dev ssr</c>, <c>prod ssr</c>,
/// <c>prod ssr - SVG format validation</c>, <c>trailing slash on the endpoint</c> and the two SSR tests of <c>support base option correctly</c>.
/// They all use the <c>core-image-ssr</c> fixture, which keeps the upstream sources (a catch-all page with a local and a remote
/// <c>&lt;Image&gt;</c>, a prerendered page, an API route and the images). Upstream calls <c>app.render()</c>; here the requests go over HTTP.
/// The class also has the one test of the <c>custom endpoint</c> group, which is the only one of the <c>dev</c> group that depends on the adapter.
/// <list type="bullet">
/// <item>The upstream prod builds set <c>image.endpoint.entrypoint</c> to <c>astro/assets/endpoint/node</c>, which reads the images from the
/// file system. This host has no file system for the engine; the adapter's <c>astro-adapter-jint/image-endpoint</c> reads them through the
/// host, and the fixture leaves the option alone. The image service is the adapter's passthrough one, which returns the image as it is
/// (the upstream tests use a stub service, <c>testImageService()</c>, that also does not transform).</item>
/// <item>The endpoint answers with the type it finds in the bytes of the image, so the upstream assertions of <c>image/webp</c> for a request of
/// <c>f=webp</c> on a JPEG are <c>image/jpeg</c> here (adapted).</item>
/// <item><c>dev ssr</c> runs the dev server with <c>base: 'some-base'</c> and asks for <c>/src/assets/penguin1.jpg?origWidth=...</c>, a
/// dev-server URL. Here the same checks run against the production build with <c>base: '/blog'</c> (the build that the base test of the
/// group also uses), and the <c>href</c> is the one that the page gives its own image: the built <c>/blog/_astro/penguin1.&lt;hash&gt;.jpg</c>
/// (adapted). The production app answers only the requests inside the base, so the API route is <c>/blog/api</c>.</item>
/// <item>The <c>badPaths</c> of <c>endpoint handle malformed requests</c> are appended to the URL as they are, and <c>new Request()</c> parses
/// the URL: it drops tabs and encodes control characters in the query. The port serializes the query the same way (<c>ParsedQuery</c>) and
/// sends it over a raw socket, which does not touch the request target. The strings are the values of the JavaScript source (where
/// <c>'\e'</c> is <c>'e'</c> and <c>'\b'</c> is a backspace). Upstream asks <c>isRemotePath()</c> of <c>@astrojs/internal-helpers</c> which of
/// them are remote (those get 403); the two that are (they start with a backslash) are listed here, as that function runs in Node.</item>
/// <item>The upstream <c>trailing slash</c> tests start a dev server on the static <c>core-image</c> fixture; the first two read the
/// <c>#local img</c> of its page. Here the page is the on-demand catch-all of the SSR fixture, built with <c>trailingSlash: 'always'</c> and
/// <c>'never'</c> (adapted); the third request (a relative pattern that is not encoded) runs as upstream on a default build.</item>
/// <item><c>has base path prefix in SSR</c> and <c>returns 403 when loading a relative pattern image</c> are in the group of the SSG base
/// option upstream but build the SSR fixture; the other five tests of that group read a static build and are listed in
/// <c>docs/conformance/core.md</c>.</item>
/// <item><c>custom endpoint works</c> runs the dev server on the static <c>core-image</c> fixture with <c>image.endpoint.entrypoint</c> set to
/// <c>./src/custom-endpoint.ts</c>. Here the same option is set on the SSR fixture (the file is a copy of the upstream one, which is not in the
/// SSR fixture upstream) and the page's own image URL is requested (adapted). The adapter only sets its endpoint when the option is
/// unset, so the user's endpoint answers.</item>
/// <item><c>allows f=svg for an actual SVG data: URI</c> is skipped (<c>GAP-CORE-5</c>): <c>fetch()</c> of the engine cannot load a
/// <c>data:</c> URL, so the endpoint answers 500. The non-upstream test <c>Endpoint_AllowsSvgFormat_ForAnSvgFile</c> shows that an SVG that the
/// endpoint can read (<c>public/pixel.svg</c>, a file that only this port has) is served with <c>image/svg+xml</c>.</item>
/// <item><c>returns 400 for local paths with parent directory segments</c> is skipped (<c>GAP-CORE-6</c>): the endpoint resolves the
/// <c>..</c> segments against the root of the client output, so it answers 404 where Node answers 400. The non-upstream test
/// <c>Endpoint_Answers404_ForParentDirectorySegments_WithoutServingAFileOutsideTheClientOutput</c> checks that no file next to the client
/// output (<c>astro-jint.json</c>, <c>server/entry.mjs</c>) can be read that way.</item>
/// </list>
/// The groups that remain (the dev server with a static site, the static builds, the errors of the asset pipeline) are explained in
/// <c>docs/conformance/core.md</c>.
/// </summary>
[Trait("Upstream", "core/core-image.test.ts")]
public class CoreImageTests(SiteHost sites)
{
    private const string Fixture = "core-image-ssr";

    private const string NotSvgMessage = "Cannot convert non-SVG source to SVG format";

    private static readonly object WithDataUrls = new { image = new { remotePatterns = new[] { new { protocol = "data" } } } };
    private static readonly object WithBase = new { @base = "/blog" };
    private static readonly object SlashAlways = new { trailingSlash = "always" };
    private static readonly object SlashNever = new { trailingSlash = "never" };
    private static readonly object WithCustomEndpoint = new { image = new { endpoint = new { route = "/_image", entrypoint = "./src/custom-endpoint.ts" } } };

    private static readonly string[] OpaqueErrors = ["Invalid request", "Forbidden", "Not Found", "Internal Server Error"];

    // The two strings of the list below for which isRemotePath() is true. They start with a backslash.
    private static readonly string[] RemotePaths =
    [
        "\\&apos;/bin/cat%20/etc/passwd\\&apos;",
        "\\&apos;/bin/cat%20/etc/shadow\\&apos;",
    ];

    private static readonly string[] BadPaths =
    [
        "../../../../../../../../../../../../etc/hosts%00",
        "../../../../../../../../../../../../etc/hosts",
        "../../boot.ini",
        "/../../../../../../../../%2A",
        "../../../../../../../../../../../../etc/passwd%00",
        "../../../../../../../../../../../../etc/passwd",
        "../../../../../../../../../../../../etc/shadow%00",
        "../../../../../../../../../../../../etc/shadow",
        "/../../../../../../../../../../etc/passwd^^",
        "/../../../../../../../../../../etc/shadow^^",
        "/../../../../../../../../../../etc/passwd",
        "/../../../../../../../../../../etc/shadow",
        "/./././././././././././etc/passwd",
        "/./././././././././././etc/shadow",
        "....................etcpasswd",
        "....................etcshadow",
        "....................etcpasswd",
        "....................etcshadow",
        "/..../..../..../..../..../..../etc/passwd",
        "/..../..../..../..../..../..../etc/shadow",
        ".\\./.\\./.\\./.\\./.\\./.\\./etc/passwd",
        ".\\./.\\./.\\./.\\./.\\./.\\./etc/shadow",
        "....................etcpasswd%00",
        "....................etcshadow%00",
        "....................etcpasswd%00",
        "....................etcshadow%00",
        "%0a/bin/cat%20/etc/passwd",
        "%0a/bin/cat%20/etc/shadow",
        "%00/etc/passwd%00",
        "%00/etc/shadow%00",
        "%00../../../../../../etc/passwd",
        "%00../../../../../../etc/shadow",
        "/../../../../../../../../../../../etc/passwd%00.jpg",
        "/../../../../../../../../../../../etc/passwd%00.html",
        "/..%c0%af../..%c0%af../..%c0%af../..%c0%af../..%c0%af../..%c0%af../etc/passwd",
        "/..%c0%af../..%c0%af../..%c0%af../..%c0%af../..%c0%af../..%c0%af../etc/shadow",
        "/%2e%2e/%2e%2e/%2e%2e/%2e%2e/%2e%2e/%2e%2e/%2e%2e/%2e%2e/%2e%2e/%2e%2e/etc/passwd,",
        "/%2e%2e/%2e%2e/%2e%2e/%2e%2e/%2e%2e/%2e%2e/%2e%2e/%2e%2e/%2e%2e/%2e%2e/etc/shadow,",
        "%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%,25%5c..%25%5c..%25%5c..%25%5c..%00",
        "/%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..,%25%5c..%25%5c..%25%5c..%25%5c..%00",
        "%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%,25%5c..%25%5c..%\t25%5c..%25%5c..%00",
        "%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%,25%5c..%25%5c..%\t\t25%5c..%25%5c..%255cboot.ini",
        "/%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..%25%5c..,%25%5c..%25%5c..%25%5c..%25%5c..winnt/desktop.ini",
        "\\&apos;/bin/cat%20/etc/passwd\\&apos;",
        "\\&apos;/bin/cat%20/etc/shadow\\&apos;",
        "../../../../../../../../conf/server.xml",
        "/../../../../../../../../bin/id|",
        "C:/inetpub/wwwroot/global.asa",
        "C:inetpubwwwrootglobal.asa",
        "C:/boot.ini",
        "C:\boot.ini",
        "../../../../../../../../../../../../localstart.asp%00",
        "../../../../../../../../../../../../localstart.asp",
        "../../../../../../../../../../../../boot.ini%00",
        "../../../../../../../../../../../../boot.ini",
        "/./././././././././././boot.ini",
        "/../../../../../../../../../../../boot.ini%00",
        "/../../../../../../../../../../../boot.ini",
        "/..../..../..../..../..../..../boot.ini",
        "/.\\./.\\./.\\./.\\./.\\./.\\./boot.ini",
        "....................\boot.ini",
        "....................\boot.ini%00",
        "....................\boot.ini",
        "/../../../../../../../../../../../boot.ini%00.html",
        "/../../../../../../../../../../../boot.ini%00.jpg",
        "/.../.../.../.../.../\t",
        "..%c0%af../..%c0%af../..%c0%af../..%c0%af../..%c0%af../..%c0%af../boot.ini",
        "/%2e%2e/%2e%2e/%2e%2e/%2e%2e/%2e%2e/%2e%2e/%2e%2e/%2e%2e/%2e%2e/%2e%2e/boot.ini",
        "../prerender/index.html",
    ];

    private static readonly string[] ParentDirectoryPaths = ["/../../../.env", "/foo/../../../.env", "/foo\\..\\..\\.env", "/%2e%2e/.env"];

    private const string PngDataUri =
        "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==";

    private const string SvgDataUri =
        "data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciPjxyZWN0IHdpZHRoPSIxIiBoZWlnaHQ9IjEiLz48L3N2Zz4=";

    /// <summary>The <c>src</c> of the <c>#local img</c> of a page of the fixture.</summary>
    private static async Task<string> LocalImageSrcAsync(ConformanceSite site, string page)
    {
        using var response = await site.GetAsync(page);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var src = (await response.ReadHtmlAsync()).QuerySelector("#local img")?.GetAttribute("src");

        Assert.NotNull(src);
        Assert.NotEmpty(src);
        return src;
    }

    /// <summary>The query of an <c>/_image</c> request for the image that the page gives itself, in the given format.</summary>
    private static async Task<string> ImageQueryAsync(ConformanceSite site, string page, string format)
    {
        var src = await LocalImageSrcAsync(site, page);
        var href = HttpUtility.ParseQueryString(src[(src.IndexOf('?') + 1)..])["href"];

        Assert.NotNull(href);
        return $"href={Uri.EscapeDataString(href)}&f={format}";
    }

    /// <summary>
    /// What <c>new Request(url)</c> makes of the query of a URL: tabs and line breaks are dropped and control characters are
    /// percent-encoded. The bad paths contain no other character that the URL parser changes, and this fails if one does.
    /// </summary>
    private static string ParsedQuery(string query)
    {
        var builder = new StringBuilder();
        foreach (var character in query)
        {
            switch (character)
            {
                case '\t' or '\n' or '\r':
                    break;
                case < ' ':
                    builder.Append('%').Append(((int)character).ToString("X2"));
                    break;
                case ' ' or '"' or '#' or '<' or '>' or '\'' or > '~':
                    throw new NotSupportedException($"The URL parser changes the character U+{(int)character:X4} in a query.");
                default:
                    builder.Append(character);
                    break;
            }
        }

        return builder.ToString();
    }

    // dev ssr

    [Fact(DisplayName = "dev ssr: serves the image at /_image")]
    public async Task Endpoint_ServesTheBuiltImage_UnderTheBase()
    {
        var site = await sites.GetAsync(Fixture, WithBase);
        var query = await ImageQueryAsync(site, "/blog/", "webp");

        using var response = await site.GetAsync($"/blog/_image?{query}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact(DisplayName = "dev ssr: returns HEAD method ok for /_image")]
    public async Task Endpoint_AnswersHead_WithoutABody()
    {
        var site = await sites.GetAsync(Fixture, WithBase);
        var query = await ImageQueryAsync(site, "/blog/", "webp");

        using var response = await site.SendAsync(HttpMethod.Head, $"/blog/_image?{query}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact(DisplayName = "dev ssr: does not interfere with query params")]
    public async Task ApiRoute_ReceivesItsQueryParameters()
    {
        var site = await sites.GetAsync(Fixture, WithBase);

        using var response = await site.GetAsync("/blog/api?src=image.png");

        Assert.Equal("An image: \"image.png\"", await response.ReadTextAsync());
    }

    [Fact(DisplayName = "dev ssr: rejects f=svg when source is not SVG in dev mode")]
    public async Task Endpoint_RejectsSvgFormat_ForAJpegSource_UnderTheBase()
    {
        var site = await sites.GetAsync(Fixture, WithBase);
        var query = await ImageQueryAsync(site, "/blog/", "svg");

        using var response = await site.GetAsync($"/blog/_image?{query}");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // prod ssr

    [Fact(DisplayName = "prod ssr: dynamic route images are built at response time")]
    public async Task OnDemandPage_ImageIsServedByTheEndpoint()
    {
        var site = await sites.GetAsync(Fixture);
        var src = await LocalImageSrcAsync(site, "/");

        using var response = await site.GetAsync(src);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "prod ssr: endpoint handle malformed requests")]
    public async Task Endpoint_AnswersMalformedHrefs_WithAnOpaqueError()
    {
        var site = await sites.GetAsync(Fixture);

        foreach (var path in BadPaths)
        {
            var response = await site.SendRawAsync("/_image?href=" + ParsedQuery(path));

            int[] expectedStatuses = RemotePaths.Contains(path) ? [403] : [400, 404, 500];
            Assert.True(
                expectedStatuses.Contains(response.StatusCode),
                $"Path \"{path}\" should return {string.Join(" or ", expectedStatuses)}, not {response.StatusCode}.");
            Assert.True(OpaqueErrors.Contains(response.Text), $"Path \"{path}\" should return an opaque error, not \"{response.Text}\".");
        }

        // Server should still be running
        using var home = await site.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);
    }

    [Fact(
        DisplayName = "prod ssr: returns 400 for local paths with parent directory segments",
        Skip = "GAP-CORE-6: the adapter's image endpoint resolves the segments against the root (/../x is /x) and answers 404 Not Found instead of 400 Invalid request.")]
    public async Task Endpoint_Rejects_LocalPathsWithParentDirectorySegments()
    {
        var site = await sites.GetAsync(Fixture);

        foreach (var path in ParentDirectoryPaths)
        {
            var response = await site.SendRawAsync("/_image?href=" + Uri.EscapeDataString(path));

            Assert.True(response.StatusCode == 400, $"Path \"{path}\" should return 400, not {response.StatusCode}.");
            Assert.Equal("Invalid request", response.Text);
        }
    }

    [Fact(DisplayName = "Not upstream: prod ssr: local paths with parent directory segments never reach a file outside the client output")]
    public async Task Endpoint_Answers404_ForParentDirectorySegments_WithoutServingAFileOutsideTheClientOutput()
    {
        var site = await sites.GetAsync(Fixture);
        // Real files that sit next to the client output. The requests below must not be able to read them.
        Assert.True(site.PathExists("astro-jint.json"));
        Assert.True(site.PathExists("server/entry.mjs"));

        foreach (var path in ParentDirectoryPaths.Concat(["/../astro-jint.json", "/../server/entry.mjs", "/foo/../../server/entry.mjs"]))
        {
            var response = await site.SendRawAsync("/_image?href=" + Uri.EscapeDataString(path));

            Assert.True(response.StatusCode == 404, $"Path \"{path}\" should return 404, not {response.StatusCode}.");
            Assert.Equal("Not Found", response.Text);
        }
    }

    [Fact(DisplayName = "prod ssr: returns 404 when a valid local image is not found")]
    public async Task Endpoint_Answers404_WhenTheLocalImageIsMissing()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/_image?href=/does-not-exist.png");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Not Found", await response.ReadTextAsync());
    }

    [Fact(DisplayName = "prod ssr: prerendered routes images are built")]
    public async Task PrerenderedPage_ImageIsInTheClientOutput()
    {
        var site = await sites.GetAsync(Fixture);
        using var page = await site.GetAsync("/prerender");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);

        var src = (await page.ReadHtmlAsync()).QuerySelector("img")?.GetAttribute("src");

        Assert.NotNull(src);
        Assert.True(site.PathExists($"client{src}"), $"The build did not write {src} to the client output.");
        // Not asserted upstream: the host serves the file
        using var image = await site.GetAsync(src);
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
    }

    [Fact(DisplayName = "prod ssr: can load images from public dir")]
    public async Task Endpoint_ServesAnImageOfThePublicDirectory()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/_image?href=/penguin3.jpg&f=webp");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact(DisplayName = "prod ssr: rejects f=svg when source is not SVG")]
    public async Task Endpoint_RejectsSvgFormat_ForAJpegSource()
    {
        var site = await sites.GetAsync(Fixture);

        // Attempt to request a non-SVG source with f=svg: this should be rejected
        using var response = await site.GetAsync("/_image?href=/penguin3.jpg&f=svg");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(NotSvgMessage, await response.ReadTextAsync());
    }

    // prod ssr - SVG format validation

    [Fact(DisplayName = "prod ssr - SVG format validation: rejects f=svg for a non-SVG data: URI in production build")]
    public async Task Endpoint_RejectsSvgFormat_ForAPngDataUri()
    {
        var site = await sites.GetAsync(Fixture, WithDataUrls);

        using var response = await site.GetAsync($"/_image?href={Uri.EscapeDataString(PngDataUri)}&f=svg");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Contains(NotSvgMessage, await response.ReadTextAsync());
    }

    [Fact(
        DisplayName = "prod ssr - SVG format validation: allows f=svg for an actual SVG data: URI in production build",
        Skip = "GAP-CORE-5: fetch() of the engine cannot load a data: URL (it fails with \"Failed to fetch\"), so the endpoint answers 500 for the SVG data URI.")]
    public async Task Endpoint_AllowsSvgFormat_ForAnSvgDataUri()
    {
        var site = await sites.GetAsync(Fixture, WithDataUrls);

        using var response = await site.GetAsync($"/_image?href={Uri.EscapeDataString(SvgDataUri)}&f=svg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact(DisplayName = "Not upstream: prod ssr - SVG format validation: allows f=svg for an actual SVG file of the public directory")]
    public async Task Endpoint_AllowsSvgFormat_ForAnSvgFile()
    {
        var site = await sites.GetAsync(Fixture, WithDataUrls);

        using var response = await site.GetAsync("/_image?href=/pixel.svg&f=svg");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<svg", await response.ReadTextAsync());
    }

    [Fact(DisplayName = "prod ssr - SVG format validation: rejects f=svg for a remote-style non-SVG URL in production build")]
    public async Task Endpoint_RejectsSvgFormat_ForALocalJpeg_WhenDataUrlsAreAllowed()
    {
        var site = await sites.GetAsync(Fixture, WithDataUrls);

        // Local path with .jpg extension: should be rejected when f=svg
        using var response = await site.GetAsync("/_image?href=/penguin3.jpg&f=svg");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    // trailing slash on the endpoint

    [Fact(DisplayName = "trailing slash on the endpoint: includes a trailing slash if trailing slash is set to always")]
    public async Task ImageUrl_HasATrailingSlash_WhenTrailingSlashIsAlways()
    {
        var site = await sites.GetAsync(Fixture, SlashAlways);

        var src = await LocalImageSrcAsync(site, "/");

        Assert.StartsWith("/_image/?", src);
    }

    [Fact(DisplayName = "trailing slash on the endpoint: does not includes a trailing slash if trailing slash is set to never")]
    public async Task ImageUrl_HasNoTrailingSlash_WhenTrailingSlashIsNever()
    {
        var site = await sites.GetAsync(Fixture, SlashNever);

        var src = await LocalImageSrcAsync(site, "/");

        Assert.StartsWith("/_image?", src);
    }

    [Fact(DisplayName = "trailing slash on the endpoint: returns 403 for /_image when requesting a relative pattern image and the parameters aren't encoded")]
    public async Task Endpoint_Answers403_ForARelativePattern_WithUnencodedParameters()
    {
        var site = await sites.GetAsync(Fixture);

        // we don't use `URLSearchParams` because the initial // will get encoded
        var response = await site.SendRawAsync("/_image?href=//secure0x.netlify.app/secure0x.svg&f=svg");

        Assert.Equal(403, response.StatusCode);
    }

    // astro:image: support base option correctly

    [Fact(DisplayName = "astro:image: support base option correctly: has base path prefix in SSR")]
    public async Task OnDemandPage_ImageUrl_HasTheBasePrefix()
    {
        var site = await sites.GetAsync(Fixture, WithBase);

        var src = await LocalImageSrcAsync(site, "/blog/");

        Assert.StartsWith("/blog", src);
        using var image = await site.GetAsync(src);
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
    }

    [Fact(DisplayName = "astro:image: support base option correctly: returns 403 when loading a relative pattern image")]
    public async Task Endpoint_Answers403_ForARelativePattern_AfterTheHomePage()
    {
        var site = await sites.GetAsync(Fixture);

        using var home = await site.GetAsync("/");
        // making sure that the app works
        Assert.Equal(HttpStatusCode.OK, home.StatusCode);

        var response = await site.SendRawAsync("/_image/?href=//secure0x.netlify.app/secure0x.svg&f=svg");

        Assert.Equal(403, response.StatusCode);
    }

    // dev: custom endpoint

    [Fact(DisplayName = "astro:image: dev: custom endpoint: custom endpoint works")]
    public async Task CustomEndpoint_AnswersInsteadOfTheAdaptersImageEndpoint()
    {
        var site = await sites.GetAsync(Fixture, WithCustomEndpoint);
        var src = await LocalImageSrcAsync(site, "/");

        using var response = await site.GetAsync(src);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("You fool! I'm not an image endpoint at all, I just return this!", await response.ReadTextAsync());
    }
}
