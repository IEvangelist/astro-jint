using System.Net;

namespace AstroJint.Sample;

/// <summary>The pages that <c>astro build</c> rendered ahead of time. The host serves them as files, without the engine pool.</summary>
public class StaticPageTests(SampleSite site)
{
    [Theory]
    [InlineData("/", "Hybrid Astro on Jint")]
    [InlineData("/about", "About")]
    [InlineData("/blog", "Blog")]
    [InlineData("/blog/hello-jint", "Hello, Jint")]
    [InlineData("/blog/static-meets-dynamic", "Static meets dynamic")]
    [InlineData("/blog/engine-pool", "The engine pool")]
    [InlineData("/products", "Products")]
    public async Task PrerenderedPagesAreServedAsFiles(string path, string heading)
    {
        using var client = site.CreateClient();

        using var response = await client.GetAsync(path);
        var page = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.Equal(heading, page.Text("h1"));
        Assert.Equal("static", page.QuerySelector("footer .badge")?.GetAttribute("data-mode"));
        Assert.Equal(["max-age=0", "must-revalidate", "public"], response.CacheControl());
        Assert.NotNull(response.Headers.ETag);
        Assert.NotNull(response.Content.Headers.LastModified);
        Assert.Null(response.Header("X-Request-Id"));
    }

    [Fact]
    public async Task APageIsTheFileThatTheBuildWrote()
    {
        using var client = site.CreateClient();

        var body = await client.GetStringAsync("/blog/hello-jint");

        Assert.Equal(await File.ReadAllTextAsync(Path.Combine(site.DistPath, "client", "blog", "hello-jint", "index.html")), body);
    }

    [Fact]
    public async Task ABuildTimeValueDoesNotChange()
    {
        using var client = site.CreateClient();

        var first = await (await client.GetAsync("/")).ReadHtmlAsync();
        await Task.Delay(50);
        var second = await (await client.GetAsync("/")).ReadHtmlAsync();

        Assert.Equal(first.Text("#built-at"), second.Text("#built-at"));
    }

    [Fact]
    public async Task ConditionalRequestsAreNotModified()
    {
        using var client = site.CreateClient();
        using var first = await client.GetAsync("/about");
        using var conditional = new HttpRequestMessage(HttpMethod.Get, "/about");
        conditional.Headers.IfNoneMatch.Add(first.Headers.ETag!);

        using var second = await client.SendAsync(conditional);

        Assert.Equal(HttpStatusCode.NotModified, second.StatusCode);
    }

    [Fact]
    public async Task BlogPostsComeFromGetStaticPaths()
    {
        using var client = site.CreateClient();

        var index = await (await client.GetAsync("/blog")).ReadHtmlAsync();

        var links = index.QuerySelectorAll("main a[href^='/blog/']").Select(link => link.GetAttribute("href")).Order().ToArray();
        Assert.Equal(["/blog/engine-pool", "/blog/hello-jint", "/blog/static-meets-dynamic"], links);
    }

    [Theory]
    [InlineData("/nothing-here")]
    [InlineData("/blog/not-a-post")]
    [InlineData("/products/1/details")]
    public async Task UnknownAddressesGetTheNotFoundPage(string path)
    {
        using var client = site.CreateClient();

        using var response = await client.GetAsync(path);
        var page = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("Page not found", page.Text("h1"));
    }

    [Fact]
    public async Task ConfiguredRedirectsAreAnswered()
    {
        using var client = site.CreateClient();

        using var response = await client.GetAsync("/news");

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal("/blog", response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task HashedAssetsAreCachedForever()
    {
        using var client = site.CreateClient();
        var home = await (await client.GetAsync("/")).ReadHtmlAsync();
        var href = home.QuerySelector("link[rel=stylesheet]")?.GetAttribute("href");

        Assert.StartsWith("/_astro/", href);
        using var response = await client.GetAsync(href);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/css; charset=utf-8", response.Content.Headers.ContentType?.ToString());
        Assert.Equal(["immutable", "max-age=31536000", "public"], response.CacheControl());
    }

    [Theory]
    [InlineData("/favicon.svg", "image/svg+xml; charset=utf-8")]
    [InlineData("/robots.txt", "text/plain; charset=utf-8")]
    public async Task FilesOfThePublicFolderAreServed(string path, string contentType)
    {
        using var client = site.CreateClient();

        using var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(contentType, response.Content.Headers.ContentType?.ToString());
    }

    [Fact]
    public async Task TheBuildOutputIsNotServedAsItIs()
    {
        using var client = site.CreateClient();

        using var manifest = await client.GetAsync("/astro-jint.json");
        using var bundle = await client.GetAsync("/server/entry.mjs");

        Assert.Equal(HttpStatusCode.NotFound, manifest.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, bundle.StatusCode);
    }
}
