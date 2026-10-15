using System.Globalization;
using System.Net;
using System.Web;

namespace AstroJint.Conformance.Vercel;

/// <summary>
/// Port of <c>packages/integrations/vercel/test/image.test.ts</c>. Upstream builds a site with <c>imageService: true</c>, so
/// that the images of the build are links to the Image Optimization API of Vercel (<c>/_vercel/image</c>), and checks those
/// links, the widths that the service accepts, the <c>images</c> section of <c>config.json</c> and, against a dev server,
/// the local image endpoint: a link, an SVG and the srcset of a responsive image.
/// <para>
/// Differences from upstream: this adapter has no image service of its own. It keeps the local service of Astro with a
/// pass-through transform (the host runs no <c>sharp</c>), so the pictures are served as they are. The images of a prerendered
/// page are files of the assets folder and those of an on-demand page are served by the <c>/_image</c> endpoint, which is what
/// the tests of the dev server ask of the endpoint, so the three of them run against the on-demand page of <c>vercel-server</c>
/// and the first two against the prerendered page of <c>vercel-static</c>. The widths of the srcset are those of Astro, not
/// the list of sizes that the Vercel API accepts, so the srcset test checks that every entry is a served image of the width
/// it announces. The three tests that are about the Vercel service (its widths, its sizes and its <c>config.json</c>) have
/// nothing to ask of this host.
/// </para>
/// </summary>
[Trait("Upstream", "vercel/image.test.ts")]
public class ImageTests(SiteHost sites)
{
    [Fact(DisplayName = "build successful")]
    public async Task TheBuildHasThePageWithTheImages()
    {
        var site = await sites.StaticAsync();

        Assert.True(site.PathExists("client/image/index.html"));
        using var response = await site.GetAsync("/image");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "has link to vercel in build with proper attributes")]
    public async Task TheBuiltImageIsAFileOfTheAssetsFolder()
    {
        var site = await sites.StaticAsync();

        var image = (await site.GetHtmlAsync("/image")).QuerySelector("#basic-image img");
        var src = image?.GetAttribute("src");

        Assert.NotNull(src);
        Assert.StartsWith("/_astro/astro.", src);
        Assert.Equal("lazy", image!.GetAttribute("loading"));
        Assert.Equal("225", image.GetAttribute("width"));
        Assert.True(site.PathExists("client" + src));
        using var file = await site.GetAsync(src);
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("image/jpeg", file.Content.Headers.ContentType?.MediaType);
        Assert.Equal("public, max-age=31536000, immutable", Assert.Single(file.HeaderValues("Cache-Control")));
    }

    [Fact(DisplayName = "supports SVGs: static output")]
    public async Task ABuiltSvgIsServed()
    {
        var site = await sites.StaticAsync();

        var src = (await site.GetHtmlAsync("/image")).QuerySelector("#svg img")?.GetAttribute("src");

        Assert.NotNull(src);
        Assert.StartsWith("/_astro/penguin.", src);
        using var file = await site.GetAsync(src);
        Assert.Equal(HttpStatusCode.OK, file.StatusCode);
        Assert.Equal("image/svg+xml", file.Content.Headers.ContentType?.MediaType);
    }

    [Fact(DisplayName = "has link to local image in dev with proper attributes")]
    public async Task TheImageOfAnOnDemandPageIsServedByTheImageEndpoint()
    {
        var site = await sites.ServerAsync();

        var image = (await site.GetHtmlAsync("/image")).QuerySelector("#basic-image img");
        var src = image?.GetAttribute("src");

        Assert.NotNull(src);
        Assert.StartsWith("/_image?href=", src);
        Assert.Equal("lazy", image!.GetAttribute("loading"));
        Assert.Equal("225", image.GetAttribute("width"));
        using var response = await site.GetAsync(src);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact(DisplayName = "supports SVGs")]
    public async Task AnSvgIsServedByTheImageEndpoint()
    {
        var site = await sites.ServerAsync();

        var src = (await site.GetHtmlAsync("/image")).QuerySelector("#svg img")?.GetAttribute("src");

        Assert.NotNull(src);
        using var response = await site.GetAsync(src);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.ToString());
    }

    [Fact(DisplayName = "generates valid srcset for responsive images")]
    public async Task TheSrcsetOfAResponsiveImageIsServed()
    {
        var site = await sites.ServerAsync();

        var srcset = (await site.GetHtmlAsync("/image")).QuerySelector("#responsive img")?.GetAttribute("srcset");

        Assert.NotNull(srcset);
        var entries = srcset.Split(", ").Select(entry => entry.Split(' ')).ToList();
        Assert.True(entries.Count > 1);
        Assert.All(entries, entry => Assert.Equal(2, entry.Length));
        var widths = entries.Select(entry => int.Parse(entry[1].TrimEnd('w'), CultureInfo.InvariantCulture)).ToList();
        Assert.Equal(widths.Order().Distinct().ToList(), widths);
        foreach (var (url, descriptor) in entries.Select(entry => (entry[0], entry[1])))
        {
            Assert.Equal(descriptor.TrimEnd('w'), HttpUtility.ParseQueryString(new Uri("http://localhost" + url).Query)["w"]);
            using var response = await site.GetAsync(url);
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("image/jpeg", response.Content.Headers.ContentType?.MediaType);
        }
    }
}
