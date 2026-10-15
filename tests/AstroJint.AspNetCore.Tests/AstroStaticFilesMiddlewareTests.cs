using System.Net;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Logging;

namespace AstroJint.AspNetCore.Tests;

public class AstroStaticFilesMiddlewareTests
{
    // The handwritten bundle answers the paths it does not know with this body, which tells the engine from a static file.
    private const string EngineNotFoundBody = "not found";

    private static async Task AssertEngineNotFoundAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(EngineNotFoundBody, await response.Content.ReadAsStringAsync());
    }

    private static string[] CacheDirectives(HttpResponseMessage response) =>
        [.. string.Join(",", response.Headers.GetValues("Cache-Control"))
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Order(StringComparer.Ordinal)];

    private static DistFolder SiteWithTwoPages(string baseFolder = "/", string trailingSlash = "ignore", string buildFormat = "directory") =>
        DistFolder.Create(baseFolder, trailingSlash, buildFormat)
            .WithClientFile("index.html", "<h1>Home</h1>")
            .WithClientFile("about/index.html", "<h1>About</h1>");

    [Theory]
    [InlineData("/", "<h1>Home</h1>")]
    [InlineData("/index.html", "<h1>Home</h1>")]
    [InlineData("/about", "<h1>About</h1>")]
    [InlineData("/about/", "<h1>About</h1>")]
    [InlineData("/about/index.html", "<h1>About</h1>")]
    [InlineData("/404.html", "<h1>Prerendered 404</h1>")]
    public async Task Pages_AreServedFromTheBuild(string path, string expectedBody)
    {
        await using var site = await TestSite.StartAsync();

        using var response = await site.Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.Equal(expectedBody, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task FilesFormat_PagesAreServedWithoutTheirExtension()
    {
        using var dist = DistFolder.Create(buildFormat: "file")
            .WithClientFile("index.html", "<h1>Home</h1>")
            .WithClientFile("about.html", "<h1>About</h1>")
            .WithClientFile("blog/first.html", "<h1>First</h1>");
        await using var site = await TestSite.StartAsync(dist);

        using var about = await site.Client.GetAsync("/about");
        using var first = await site.Client.GetAsync("/blog/first");
        using var explicitExtension = await site.Client.GetAsync("/about.html");

        Assert.Equal("<h1>About</h1>", await about.Content.ReadAsStringAsync());
        Assert.Equal("<h1>First</h1>", await first.Content.ReadAsStringAsync());
        Assert.Equal("<h1>About</h1>", await explicitExtension.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task StaticRequests_NeverStartAnEngine()
    {
        await using var site = await TestSite.StartAsync();

        foreach (var path in new[] { "/", "/about", "/favicon.svg", "/_astro/app.abc123.js" })
        {
            using var response = await site.Client.GetAsync(path);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        Assert.Equal(0, site.Astro.Pool.Reserved);

        using var onDemand = await site.Client.GetAsync("/echo");

        Assert.Equal(HttpStatusCode.OK, onDemand.StatusCode);
        Assert.Equal(1, site.Astro.Pool.Reserved);
    }

    [Theory]
    [InlineData("/", "text/html; charset=utf-8")]
    [InlineData("/favicon.svg", "image/svg+xml; charset=utf-8")]
    [InlineData("/data.json", "application/json; charset=utf-8")]
    [InlineData("/robots.txt", "text/plain; charset=utf-8")]
    [InlineData("/_astro/app.abc123.js", "text/javascript; charset=utf-8")]
    [InlineData("/sitemap-index.xml", "text/xml; charset=utf-8")]
    [InlineData("/site.webmanifest", "application/manifest+json; charset=utf-8")]
    [InlineData("/logo.png", "image/png")]
    [InlineData("/font.woff2", "font/woff2")]
    [InlineData("/blob.bin", "application/octet-stream")]
    [InlineData("/mystery.unknownext", "application/octet-stream")]
    public async Task ContentTypes_FollowTheExtension(string path, string expectedContentType)
    {
        using var dist = DistFolder.CreateDefault()
            .WithClientFile("sitemap-index.xml", "<sitemapindex/>")
            .WithClientFile("site.webmanifest", "{}")
            .WithClientFile("logo.png", [1, 2, 3])
            .WithClientFile("font.woff2", [1, 2, 3])
            .WithClientFile("mystery.unknownext", "x");
        await using var site = await TestSite.StartAsync(dist);

        using var response = await site.Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedContentType, response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task HashedAssets_AreCachedForever()
    {
        await using var site = await TestSite.StartAsync();

        using var response = await site.Client.GetAsync("/_astro/app.abc123.js");

        Assert.Equal(["immutable", "max-age=31536000", "public"], CacheDirectives(response));
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/about")]
    [InlineData("/favicon.svg")]
    [InlineData("/robots.txt")]
    public async Task EverythingElse_IsRevalidatedOnEveryUse(string path)
    {
        await using var site = await TestSite.StartAsync();

        using var response = await site.Client.GetAsync(path);

        Assert.Equal(["max-age=0", "must-revalidate", "public"], CacheDirectives(response));
    }

    [Fact]
    public async Task Responses_CarryValidatorsAndAdvertiseRanges()
    {
        await using var site = await TestSite.StartAsync();

        using var response = await site.Client.GetAsync("/about");

        Assert.NotNull(response.Headers.ETag);
        Assert.False(response.Headers.ETag.IsWeak);
        Assert.NotNull(response.Content.Headers.LastModified);
        Assert.Contains("bytes", response.Headers.AcceptRanges);
    }

    [Fact]
    public async Task IfNoneMatch_WithTheCurrentEntityTag_Returns304()
    {
        await using var site = await TestSite.StartAsync();
        using var first = await site.Client.GetAsync("/about");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/about");
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);

        using var response = await site.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
        Assert.Equal(first.Headers.ETag, response.Headers.ETag);
        Assert.Equal(["max-age=0", "must-revalidate", "public"], CacheDirectives(response));
    }

    [Fact]
    public async Task IfNoneMatch_WithAnotherEntityTag_ServesTheFile()
    {
        await using var site = await TestSite.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/about");
        request.Headers.IfNoneMatch.Add(new EntityTagHeaderValue("\"stale\""));

        using var response = await site.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("<h1>About</h1>", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task IfModifiedSince_WithTheLastModifiedDate_Returns304()
    {
        await using var site = await TestSite.StartAsync();
        using var first = await site.Client.GetAsync("/about");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/about");
        request.Headers.IfModifiedSince = first.Content.Headers.LastModified;

        using var response = await site.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task IfModifiedSince_BeforeTheLastModifiedDate_ServesTheFile()
    {
        await using var site = await TestSite.StartAsync();
        using var first = await site.Client.GetAsync("/about");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/about");
        request.Headers.IfModifiedSince = first.Content.Headers.LastModified!.Value.AddDays(-1);

        using var response = await site.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("<h1>About</h1>", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Head_SendsTheHeadersOfGetWithoutTheBody()
    {
        await using var site = await TestSite.StartAsync();
        using var get = await site.Client.GetAsync("/");
        using var request = new HttpRequestMessage(HttpMethod.Head, "/");

        using var head = await site.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, head.StatusCode);
        Assert.Equal(13L, head.Content.Headers.ContentLength);
        Assert.Equal(get.Content.Headers.ContentType, head.Content.Headers.ContentType);
        Assert.Equal(get.Headers.ETag, head.Headers.ETag);
        Assert.Empty(await head.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Range_ServesPartialContent()
    {
        await using var site = await TestSite.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/blob.bin");
        request.Headers.Range = new RangeHeaderValue(10, 19);

        using var response = await site.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal("bytes 10-19/1000", response.Content.Headers.ContentRange?.ToString());
        Assert.Equal(10L, response.Content.Headers.ContentLength);
        Assert.Equal(Enumerable.Range(10, 10).Select(i => (byte)i).ToArray(), await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Range_CanAskForTheLastBytes()
    {
        await using var site = await TestSite.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/blob.bin");
        request.Headers.Range = new RangeHeaderValue(null, 5);

        using var response = await site.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal("bytes 995-999/1000", response.Content.Headers.ContentRange?.ToString());
        Assert.Equal(Enumerable.Range(995, 5).Select(i => (byte)i).ToArray(), await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Range_BeyondTheEnd_Returns416()
    {
        await using var site = await TestSite.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/blob.bin");
        request.Headers.Range = new RangeHeaderValue(2000, null);

        using var response = await site.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, response.StatusCode);
        Assert.Equal("bytes */1000", response.Content.Headers.ContentRange?.ToString());
    }

    [Theory]
    [InlineData("/_astro/app.abc123.js")]
    [InlineData("/about")]
    public async Task IfMatch_WithAnotherEntityTag_Returns412WithoutCacheHeaders(string path)
    {
        await using var site = await TestSite.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.IfMatch.Add(new EntityTagHeaderValue("\"stale\""));

        using var response = await site.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.False(response.Headers.Contains("Cache-Control"));
    }

    [Theory]
    [InlineData("/_astro/app.abc123.js")]
    [InlineData("/about")]
    public async Task IfMatch_ThatCannotBeParsed_Returns412WithoutCacheHeaders(string path)
    {
        await using var site = await TestSite.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.TryAddWithoutValidation("If-Match", "xxx");

        using var response = await site.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.False(response.Headers.Contains("Cache-Control"));
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task IfMatch_WithTheCurrentEntityTag_ServesTheFile()
    {
        await using var site = await TestSite.StartAsync();
        using var first = await site.Client.GetAsync("/about");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/about");
        request.Headers.IfMatch.Add(first.Headers.ETag!);

        using var response = await site.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("<h1>About</h1>", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task IfMatch_WithAnyEntityTag_ServesTheFile()
    {
        await using var site = await TestSite.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/about");
        request.Headers.TryAddWithoutValidation("If-Match", "*");

        using var response = await site.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Range_ThatFails_IsNeverCached()
    {
        await using var site = await TestSite.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/_astro/app.abc123.js");
        request.Headers.Range = new RangeHeaderValue(2000, null);

        using var response = await site.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.RequestedRangeNotSatisfiable, response.StatusCode);
        Assert.False(response.Headers.Contains("Cache-Control"));
    }

    [Fact]
    public async Task Range_ThatSucceeds_IsCachedLikeTheFile()
    {
        await using var site = await TestSite.StartAsync();
        using var request = new HttpRequestMessage(HttpMethod.Get, "/_astro/app.abc123.js");
        request.Headers.Range = new RangeHeaderValue(0, 3);

        using var response = await site.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.PartialContent, response.StatusCode);
        Assert.Equal(["immutable", "max-age=31536000", "public"], CacheDirectives(response));
    }

    [Theory]
    [InlineData("always", "/about", "/about/")]
    [InlineData("always", "/about?x=1&y=%20z", "/about/?x=1&y=%20z")]
    [InlineData("never", "/about/", "/about")]
    [InlineData("never", "/about/?x=1&y=%20z", "/about?x=1&y=%20z")]
    public async Task TrailingSlash_RedirectsPermanentlyAndKeepsTheQuery(string trailingSlash, string requestPath, string expectedLocation)
    {
        using var dist = SiteWithTwoPages(trailingSlash: trailingSlash);
        await using var site = await TestSite.StartAsync(dist);

        using var response = await site.Client.GetAsync(requestPath);

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal(expectedLocation, response.Headers.Location?.OriginalString);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task TrailingSlash_RedirectsHeadRequestsToo()
    {
        using var dist = SiteWithTwoPages(trailingSlash: "always");
        await using var site = await TestSite.StartAsync(dist);
        using var request = new HttpRequestMessage(HttpMethod.Head, "/about");

        using var response = await site.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("/about/", response.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData("always", "/")]
    [InlineData("always", "/about/")]
    [InlineData("always", "/robots.txt")]
    [InlineData("always", "/_astro/app.abc123.js")]
    [InlineData("never", "/")]
    [InlineData("never", "/about")]
    [InlineData("never", "/robots.txt")]
    public async Task TrailingSlash_LeavesMatchingRequestsAlone(string trailingSlash, string path)
    {
        using var dist = SiteWithTwoPages(trailingSlash: trailingSlash)
            .WithClientFile("robots.txt", "User-agent: *")
            .WithClientFile("_astro/app.abc123.js", "console.log('app');");
        await using var site = await TestSite.StartAsync(dist);

        using var response = await site.Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData("my page", "/my%20page")]
    [InlineData("café", "/caf%C3%A9")]
    public async Task PercentEncodedPaths_AreDecodedToFindTheFile(string folder, string encodedPath)
    {
        using var dist = DistFolder.Create(trailingSlash: "always").WithClientFile($"{folder}/index.html", "<h1>Page</h1>");
        await using var site = await TestSite.StartAsync(dist);

        using var page = await site.Client.GetAsync(encodedPath + "/");
        using var redirect = await site.Client.GetAsync(encodedPath);

        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        Assert.Equal("<h1>Page</h1>", await page.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.MovedPermanently, redirect.StatusCode);
        Assert.Equal(encodedPath + "/", redirect.Headers.Location?.OriginalString);
    }

    [Theory]
    [InlineData("/ABOUT")]
    [InlineData("/About/")]
    [InlineData("/ROBOTS.TXT")]
    public async Task Paths_AreCaseSensitive(string path)
    {
        await using var site = await TestSite.StartAsync();

        using var response = await site.Client.GetAsync(path);

        await AssertEngineNotFoundAsync(response);
    }

    [Theory]
    [InlineData("/docs", "<h1>Docs</h1>")]
    [InlineData("/docs/", "<h1>Docs</h1>")]
    [InlineData("/docs/guide", "<h1>Guide</h1>")]
    [InlineData("/docs/guide/", "<h1>Guide</h1>")]
    [InlineData("/docs/_astro/app.js", "console.log(1)")]
    public async Task Base_IsStrippedToFindTheFile(string path, string expectedBody)
    {
        using var dist = DistFolder.Create(baseFolder: "/docs")
            .WithClientFile("index.html", "<h1>Docs</h1>")
            .WithClientFile("guide/index.html", "<h1>Guide</h1>")
            .WithClientFile("_astro/app.js", "console.log(1)");
        await using var site = await TestSite.StartAsync(dist);

        using var response = await site.Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedBody, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/guide")]
    [InlineData("/_astro/app.js")]
    [InlineData("/documents")]
    public async Task Base_FilesAreNotServedOutsideOfIt(string path)
    {
        using var dist = DistFolder.Create(baseFolder: "/docs")
            .WithClientFile("index.html", "<h1>Docs</h1>")
            .WithClientFile("guide/index.html", "<h1>Guide</h1>")
            .WithClientFile("_astro/app.js", "console.log(1)");
        await using var site = await TestSite.StartAsync(dist);

        using var response = await site.Client.GetAsync(path);

        await AssertEngineNotFoundAsync(response);
    }

    [Theory]
    [InlineData("/docs", "/docs/")]
    [InlineData("/docs/guide", "/docs/guide/")]
    public async Task Base_IsKeptInRedirects(string path, string expectedLocation)
    {
        using var dist = DistFolder.Create(baseFolder: "/docs", trailingSlash: "always")
            .WithClientFile("index.html", "<h1>Docs</h1>")
            .WithClientFile("guide/index.html", "<h1>Guide</h1>");
        await using var site = await TestSite.StartAsync(dist);

        using var response = await site.Client.GetAsync(path + "?x=1");

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal(expectedLocation + "?x=1", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task Immutable_AppliesBehindABase()
    {
        using var dist = DistFolder.Create(baseFolder: "/docs").WithClientFile("_astro/app.js", "console.log(1)");
        await using var site = await TestSite.StartAsync(dist);

        using var response = await site.Client.GetAsync("/docs/_astro/app.js");

        Assert.Equal(["immutable", "max-age=31536000", "public"], CacheDirectives(response));
    }

    private static DistFolder SiteWithStaticHeaders(string baseFolder = "/", string trailingSlash = "ignore") =>
        SiteWithTwoPages(baseFolder, trailingSlash)
            .WithClientFile("robots.txt", "User-agent: *")
            .WithStaticHeaders("index.html", ("Content-Security-Policy", "script-src 'self' 'sha256-home'"))
            .WithStaticHeaders("about/index.html", ("Content-Security-Policy", "script-src 'self' 'sha256-about'"), ("X-Page", "about"));

    [Theory]
    [InlineData("/", "script-src 'self' 'sha256-home'")]
    [InlineData("/index.html", "script-src 'self' 'sha256-home'")]
    [InlineData("/about", "script-src 'self' 'sha256-about'")]
    [InlineData("/about/", "script-src 'self' 'sha256-about'")]
    [InlineData("/about/index.html", "script-src 'self' 'sha256-about'")]
    public async Task StaticHeaders_AreSentWithThePageUnderEveryUrlOfIt(string path, string expectedPolicy)
    {
        using var dist = SiteWithStaticHeaders();
        await using var site = await TestSite.StartAsync(dist);

        using var response = await site.Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([expectedPolicy], response.Headers.GetValues("Content-Security-Policy"));
    }

    [Fact]
    public async Task StaticHeaders_AreSentAsTheyAre()
    {
        using var dist = SiteWithStaticHeaders();
        await using var site = await TestSite.StartAsync(dist);

        using var response = await site.Client.GetAsync("/about");

        Assert.Equal(["about"], response.Headers.GetValues("X-Page"));
        Assert.Equal("<h1>About</h1>", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task StaticHeaders_AreSentWithHeadRequestsToo()
    {
        using var dist = SiteWithStaticHeaders();
        await using var site = await TestSite.StartAsync(dist);
        using var request = new HttpRequestMessage(HttpMethod.Head, "/");

        using var response = await site.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(["script-src 'self' 'sha256-home'"], response.Headers.GetValues("Content-Security-Policy"));
        Assert.Empty(await response.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task StaticHeaders_ReplaceTheHeadersOfTheSameName()
    {
        using var dist = SiteWithStaticHeaders();
        await using var site = await TestSite.StartAsync(dist, configureApp: app => app.Use((context, next) =>
        {
            context.Response.Headers.ContentSecurityPolicy = "default-src *";
            return next(context);
        }));

        using var response = await site.Client.GetAsync("/");

        Assert.Equal(["script-src 'self' 'sha256-home'"], response.Headers.GetValues("Content-Security-Policy"));
    }

    [Theory]
    [InlineData("/robots.txt")]
    [InlineData("/blog/")]
    public async Task StaticHeaders_AreOnlySentWithTheirOwnPage(string path)
    {
        using var dist = SiteWithStaticHeaders().WithClientFile("blog/index.html", "<h1>Blog</h1>");
        await using var site = await TestSite.StartAsync(dist);

        using var response = await site.Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(response.Headers.Contains("Content-Security-Policy"));
    }

    [Fact]
    public async Task StaticHeaders_AreNotSentWithRedirects()
    {
        using var dist = SiteWithStaticHeaders(trailingSlash: "always");
        await using var site = await TestSite.StartAsync(dist);

        using var response = await site.Client.GetAsync("/about");

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.False(response.Headers.Contains("Content-Security-Policy"));
    }

    [Theory]
    [InlineData("/docs", "script-src 'self' 'sha256-home'")]
    [InlineData("/docs/", "script-src 'self' 'sha256-home'")]
    [InlineData("/docs/about", "script-src 'self' 'sha256-about'")]
    [InlineData("/docs/about/index.html", "script-src 'self' 'sha256-about'")]
    public async Task StaticHeaders_FollowTheFileBehindABase(string path, string expectedPolicy)
    {
        using var dist = SiteWithStaticHeaders(baseFolder: "/docs");
        await using var site = await TestSite.StartAsync(dist);

        using var response = await site.Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal([expectedPolicy], response.Headers.GetValues("Content-Security-Policy"));
    }

    [Fact]
    public async Task StaticHeaders_AreSentWithNotModifiedResponses()
    {
        using var dist = SiteWithStaticHeaders();
        await using var site = await TestSite.StartAsync(dist);
        using var first = await site.Client.GetAsync("/");
        using var request = new HttpRequestMessage(HttpMethod.Get, "/");
        request.Headers.IfNoneMatch.Add(first.Headers.ETag!);

        using var response = await site.Client.SendAsync(request);

        Assert.Equal(HttpStatusCode.NotModified, response.StatusCode);
        Assert.Equal(["script-src 'self' 'sha256-home'"], response.Headers.GetValues("Content-Security-Policy"));
    }

    [Fact]
    public async Task StaticHeaders_AreNotSentForFilesThatDisappearedAfterStartup()
    {
        using var dist = SiteWithStaticHeaders();
        await using var site = await TestSite.StartAsync(dist, options => options.StaticFileCacheBytes = 0);
        File.Delete(Path.Combine(dist.ClientPath, "index.html"));

        using var response = await site.Client.GetAsync("/");

        await AssertEngineNotFoundAsync(response);
        Assert.False(response.Headers.Contains("Content-Security-Policy"));
    }

    [Fact]
    public async Task PathBase_IsPartOfThePublicPath()
    {
        using var dist = SiteWithTwoPages("/app", "always");
        await using var site = await TestSite.StartAsync(dist, configureApp: app =>
        {
            app.UsePathBase("/app");
            app.UseRouting();
        });

        using var home = await site.Client.GetAsync("/app/");
        using var about = await site.Client.GetAsync("/app/about/");
        using var redirect = await site.Client.GetAsync("/app/about?x=1");
        using var outside = await site.Client.GetAsync("/about/");

        Assert.Equal("<h1>Home</h1>", await home.Content.ReadAsStringAsync());
        Assert.Equal("<h1>About</h1>", await about.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.MovedPermanently, redirect.StatusCode);
        Assert.Equal("/app/about/?x=1", redirect.Headers.Location?.OriginalString);
        await AssertEngineNotFoundAsync(outside);
    }

    [Theory]
    [InlineData("/about", "native about")]
    [InlineData("/robots.txt", "native robots")]
    [InlineData("/healthz", "healthy")]
    public async Task NativeEndpoints_WinOverPrerenderedFiles(string path, string expectedBody)
    {
        await using var site = await TestSite.StartAsync(configureApp: MapNativeEndpoints);

        using var response = await site.Client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(expectedBody, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task NativeEndpoints_LeaveTheRestOfTheSiteAlone()
    {
        await using var site = await TestSite.StartAsync(configureApp: MapNativeEndpoints);

        using var home = await site.Client.GetAsync("/");
        using var onDemand = await site.Client.GetAsync("/echo");

        Assert.Equal("<h1>Home</h1>", await home.Content.ReadAsStringAsync());
        Assert.Equal(HttpStatusCode.OK, onDemand.StatusCode);
    }

    [Fact]
    public async Task NativeEndpoints_OnlyClaimTheirOwnMethods()
    {
        await using var site = await TestSite.StartAsync(configureApp: app => app.MapPost("/about", () => "posted"));

        using var page = await site.Client.GetAsync("/about");
        using var post = await site.Client.PostAsync("/about", new StringContent(""));

        Assert.Equal("<h1>About</h1>", await page.Content.ReadAsStringAsync());
        Assert.Equal("posted", await post.Content.ReadAsStringAsync());
    }

    private static void MapNativeEndpoints(WebApplication app)
    {
        app.MapGet("/about", () => "native about");
        app.MapGet("/robots.txt", () => "native robots");
        app.MapGet("/healthz", () => "healthy");
    }

    [Fact]
    public async Task OnDemandPages_GoToTheEngine()
    {
        await using var site = await TestSite.StartAsync();

        var echo = await site.GetJsonAsync("/echo?x=1");

        Assert.Equal("GET", echo.GetProperty("method").GetString());
        Assert.Equal("http://localhost/echo?x=1", echo.GetProperty("url").GetString());
    }

    [Fact]
    public async Task UnknownPaths_GoToTheEngine()
    {
        await using var site = await TestSite.StartAsync();

        using var response = await site.Client.GetAsync("/nothing-here");

        await AssertEngineNotFoundAsync(response);
    }

    [Theory]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("PATCH")]
    [InlineData("DELETE")]
    [InlineData("OPTIONS")]
    public async Task OtherMethods_GoToTheEngineEvenForPrerenderedPaths(string method)
    {
        await using var site = await TestSite.StartAsync();
        using var request = new HttpRequestMessage(new HttpMethod(method), "/about");

        using var response = await site.Client.SendAsync(request);

        await AssertEngineNotFoundAsync(response);
    }

    [Fact]
    public async Task WellKnownFiles_AreServed()
    {
        await using var site = await TestSite.StartAsync();

        using var response = await site.Client.GetAsync("/.well-known/security.txt");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Contact: mailto:security@example.test", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("/private/.env")]
    [InlineData("/.env")]
    [InlineData("/astro-jint.json")]
    [InlineData("/server/entry.mjs")]
    [InlineData("/dist/astro-jint.json")]
    [InlineData("/client/index.html")]
    [InlineData("/..%2fastro-jint.json")]
    [InlineData("/%2e%2e/astro-jint.json")]
    public async Task OtherFilesOfTheBuild_AreNeverServed(string path)
    {
        await using var site = await TestSite.StartAsync();

        using var response = await site.Client.GetAsync(path);

        await AssertEngineNotFoundAsync(response);
    }

    [Fact]
    public async Task FilesThatAppearAfterStartup_AreNotServed()
    {
        using var dist = DistFolder.CreateDefault();
        await using var site = await TestSite.StartAsync(dist);
        dist.WithClientFile("late.txt", "late");

        using var response = await site.Client.GetAsync("/late.txt");

        await AssertEngineNotFoundAsync(response);
    }

    [Fact]
    public async Task FilesThatDisappearAfterStartup_GoToTheEngine()
    {
        using var dist = DistFolder.CreateDefault();
        await using var site = await TestSite.StartAsync(dist, options => options.StaticFileCacheBytes = 0);
        File.Delete(Path.Combine(dist.ClientPath, "robots.txt"));

        using var response = await site.Client.GetAsync("/robots.txt");

        await AssertEngineNotFoundAsync(response);
        Assert.DoesNotContain(site.Logs.Entries, entry => entry.Level >= LogLevel.Error);
    }
}
