using System.Net;

namespace AstroJint.Conformance.CoreBuild;

/// <summary>
/// Port of <c>packages/astro/test/image-deletion.test.ts</c>: the originals of images that are only used for optimization are
/// deleted from the build, and every other image is kept. Fixtures: <c>cb-image-deletion</c> (static, a copy of
/// <c>core-image-deletion</c> that also installs the MDX and Markdoc integrations) and <c>cb-image-deletion-ssr</c>.
/// <para>
/// Differences from upstream: <c>fixture.glob('_astro/x.*.*')</c> lists <c>dist/client/_astro</c> (the Jint layout). The image
/// service is a stub of upstream's <c>testImageService</c> (the file is a test helper of the Astro repository): it keeps the
/// bytes and only sets the format. Every test is stricter than upstream: it also checks which extension is left and requests
/// the images that the page links (or that the build kept) from the host, which has to serve them with the right content type
/// and the immutable cache header. The test adapter's <c>loadTestAdapterApp().render()</c> of the last SSR test is a request
/// to the host, which also has to answer the <c>/_image</c> URL of the page with an image: the host does not transcode, so
/// <c>f=webp</c> for a JPEG answers the stored JPEG.
/// </para>
/// <para>
/// The prerenderer that wraps the default one (<c>cb: { wrappingPrerenderer: true }</c> of the fixture) is adapted. Upstream
/// builds without an adapter, which the harness cannot host. With an adapter that resolves its own entrypoint Astro leaves
/// the prerender entrypoint out as soon as <c>setPrerenderer</c> is called, so the integration puts it back in
/// <c>astro:build:setup</c>; and Astro 7.3.8 hands the wrapper the paths themselves from <c>getStaticPaths()</c> where the Astro
/// of upstream hands <c>{ paths, metadata }</c>, so the wrapper passes on both shapes. "generates images before teardown" is not
/// ported: Astro 7.3.8 tears the prerenderer down before it generates the optimized images (upstream reordered it later), so the
/// assertion cannot hold with any adapter; the build log of the wrapped build shows the count at teardown.
/// </para>
/// </summary>
[Trait("Upstream", "core/image-deletion.test.ts")]
public class ImageDeletionTests(SiteHost sites)
{
    private const string Fixture = "cb-image-deletion";
    private const string SsrFixture = "cb-image-deletion-ssr";

    private static string[] Glob(ConformanceSite site, string pattern) =>
        [.. Directory.GetFiles(Path.Combine(site.DistPath, "client", "_astro"), pattern).Select(file => Path.GetFileName(file))];

    private static void AssertExtensions(string[] files, params string[] extensions) =>
        Assert.Equal(extensions, files.Select(file => Path.GetExtension(file)).Order(StringComparer.Ordinal).ToArray());

    /// <summary>Checks that <paramref name="page"/> links the built <paramref name="file"/> and that the host serves it.</summary>
    private static async Task AssertLinkedAndServedAsync(ConformanceSite site, string page, string file)
    {
        var src = $"/_astro/{file}";
        var html = await site.GetHtmlAsync(page);

        Assert.Contains(html.QuerySelectorAll("img"), img => img.GetAttribute("src") == src);
        await PageAssets.AssertServedAsync(site, src);
    }

    private async Task<ConformanceSite> WrappedAsync()
    {
        var site = await sites.GetAsync(Fixture, new { cb = new { wrappingPrerenderer = true } });
        Assert.Contains("onlyone images at teardown", site.BuildLog);
        return site;
    }

    private static async Task AssertImageFromGetStaticPathsAsync(ConformanceSite site)
    {
        var html = await site.GetHtmlAsync("/paths/one/");
        var src = html.QuerySelector("#from-paths")!.GetAttribute("src")!;

        Assert.Matches(@"^/_astro/staticPaths\.[^_/]+_[^_/]+\.webp\z", src);
        Assert.True(site.PathExists($"client{src}"));
        Assert.Single(Glob(site, "staticPaths.*.*"));
        await PageAssets.AssertServedAsync(site, src);
    }

    [Fact(DisplayName = "build ssg: should delete images that are only used for optimization")]
    public async Task Ssg_ImagesOnlyUsedForOptimization_AreDeleted()
    {
        var site = await sites.GetAsync(Fixture);

        var images = Glob(site, "onlyone.*.*");

        AssertExtensions(images, ".webp");
        await AssertLinkedAndServedAsync(site, "/", images[0]);
    }

    [Fact(DisplayName = "build ssg: should not delete images that are used in other contexts")]
    public async Task Ssg_ImagesUsedInOtherContexts_AreKept()
    {
        var site = await sites.GetAsync(Fixture);

        var images = Glob(site, "twoofus.*.*");

        AssertExtensions(images, ".jpg", ".webp");
        foreach (var image in images)
        {
            await AssertLinkedAndServedAsync(site, "/", image);
        }
    }

    [Fact(DisplayName = "build ssg: should not delete images that are also used through query params")]
    public async Task Ssg_ImagesAlsoUsedThroughQueryParams_AreKept()
    {
        var site = await sites.GetAsync(Fixture);

        var images = Glob(site, "url.*.*");

        AssertExtensions(images, ".jpg", ".webp");
        foreach (var image in images)
        {
            await AssertLinkedAndServedAsync(site, "/", image);
        }
    }

    [Fact(DisplayName = "build ssg: should delete MDX images only used for optimization")]
    public async Task Ssg_MdxImagesOnlyUsedForOptimization_AreDeleted()
    {
        var site = await sites.GetAsync(Fixture);

        var images = Glob(site, "mdxDontExist.*.*");

        AssertExtensions(images, ".webp");
        await AssertLinkedAndServedAsync(site, "/blog/mdx/", images[0]);
    }

    [Fact(DisplayName = "build ssg: should always keep Markdoc images")]
    public async Task Ssg_MarkdocImages_AreAlwaysKept()
    {
        var site = await sites.GetAsync(Fixture);

        var images = Glob(site, "markdocStillExists.*.*");

        AssertExtensions(images, ".jpg", ".webp");
        await AssertLinkedAndServedAsync(site, "/blog/markdoc/", images.Single(image => image.EndsWith(".webp")));
        await PageAssets.AssertServedAsync(site, $"/_astro/{images.Single(image => image.EndsWith(".jpg"))}");
    }

    [Fact(DisplayName = "build ssg: should generate images optimized in getStaticPaths")]
    public async Task Ssg_ImagesOptimizedInGetStaticPaths_AreGenerated()
    {
        var site = await sites.GetAsync(Fixture);

        await AssertImageFromGetStaticPathsAsync(site);
    }

    [Fact(DisplayName = "build ssg: should generate images optimized outside of a render")]
    public async Task Ssg_ImagesOptimizedOutsideOfARender_AreGenerated()
    {
        var site = await sites.GetAsync(Fixture);

        var html = await site.GetHtmlAsync("/outside-render/");
        var src = html.QuerySelector("#outside-render")!.GetAttribute("src")!;

        Assert.Matches(@"^/_astro/outsideRender\.[^_/]+_[^_/]+\.webp\z", src);
        Assert.True(site.PathExists($"client{src}"));
        Assert.Single(Glob(site, "outsideRender.*.*"));
        await PageAssets.AssertServedAsync(site, src);
    }

    [Fact(DisplayName = "build ssg with a prerenderer wrapping the default one: builds only the paths the wrapper returns")]
    public async Task Wrapped_BuildsOnlyThePathsTheWrapperReturns()
    {
        var site = await WrappedAsync();

        Assert.False(site.PathExists("client/outside-render/index.html"));
        Assert.True(site.PathExists("client/index.html"));
        using var dropped = await site.GetAsync("/outside-render/");
        Assert.Equal(HttpStatusCode.NotFound, dropped.StatusCode);
        using var kept = await site.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, kept.StatusCode);
    }

    [Fact(DisplayName = "build ssg with a prerenderer wrapping the default one: generates images with the default prerenderer's image service")]
    public async Task Wrapped_GeneratesImagesWithTheDefaultPrerenderersImageService()
    {
        var site = await WrappedAsync();

        AssertExtensions(Glob(site, "onlyone.*.webp"), ".webp");
        var images = Glob(site, "onlyone.*.*");
        AssertExtensions(images, ".webp");
        await AssertLinkedAndServedAsync(site, "/", images[0]);
    }

    [Fact(DisplayName = "build ssg with a prerenderer wrapping the default one: generates images from getImage() in getStaticPaths()")]
    public async Task Wrapped_GeneratesImagesFromGetImageInGetStaticPaths()
    {
        var site = await WrappedAsync();

        await AssertImageFromGetStaticPathsAsync(site);
    }

    [Fact(DisplayName = "build ssr: should delete prerendered images that are only used for optimization")]
    public async Task Ssr_PrerenderedImagesOnlyUsedForOptimization_AreDeleted()
    {
        var site = await sites.GetAsync(SsrFixture);

        var images = Glob(site, "onlyone.*.*");

        AssertExtensions(images, ".webp");
        await AssertLinkedAndServedAsync(site, "/prerendered/", images[0]);
    }

    [Fact(DisplayName = "build ssr: should not delete prerendered images that are used in other contexts")]
    public async Task Ssr_PrerenderedImagesUsedInOtherContexts_AreKept()
    {
        var site = await sites.GetAsync(SsrFixture);

        var images = Glob(site, "twoofus.*.*");

        AssertExtensions(images, ".jpg", ".webp");
        foreach (var image in images)
        {
            await AssertLinkedAndServedAsync(site, "/prerendered/", image);
        }
    }

    [Fact(DisplayName = "build ssr: should not delete images that are used in both a prerendered and an SSR page")]
    public async Task Ssr_ImagesUsedInAPrerenderedAndAnSsrPage_AreKept()
    {
        var site = await sites.GetAsync(SsrFixture);

        var images = Glob(site, "shared.*.*");

        AssertExtensions(images, ".jpg", ".webp");
        await AssertLinkedAndServedAsync(site, "/prerendered/", images.Single(image => image.EndsWith(".webp")));
        await AssertLinkedAndServedAsync(site, "/", images.Single(image => image.EndsWith(".jpg")));
    }

    [Fact(DisplayName = "build ssr: should optimize images on demand once the build is done")]
    public async Task Ssr_ImagesAreOptimizedOnDemandOnceTheBuildIsDone()
    {
        var site = await sites.GetAsync(SsrFixture);

        var html = await site.GetHtmlAsync("/ssr-image");
        var src = html.QuerySelector("img")!.GetAttribute("src")!;

        Assert.Matches(@"^/_image\?", src);
        using var optimized = await PageAssets.GetServedAsync(site, src);
        Assert.StartsWith("image/", optimized.Content.Headers.ContentType?.ToString());
    }
}
