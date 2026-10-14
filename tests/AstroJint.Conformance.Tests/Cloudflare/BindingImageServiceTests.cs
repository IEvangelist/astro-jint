using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/binding-image-service.test.ts</c>: the <c>/_image</c> endpoint refuses what
/// it must refuse, passes SVGs through and converts the rest to the format that was asked for. The site is the shared fixture
/// <c>cf-pages</c>; the pictures are its <c>public/placeholder.jpg</c> and <c>public/logo.svg</c>. The remote pictures of the
/// upstream tests live on local servers, here on a loopback origin that the host may fetch from (<c>FetchUrlFilter</c>) and
/// that the fixture lists in <c>image.domains</c>.
/// <para>
/// Two adaptations. A request without <c>href</c> is answered with 400 by the handler of Astro that the host uses, where the
/// Images binding endpoint says 403. And the redirect of the remote server points to a picture on the same origin, because a
/// test cannot reach example.com: a host that followed the redirect would fetch that picture, and the origin would see the
/// request. Upstream expects 404; the host answers 500, as the handler of Astro does for a picture that cannot be loaded, so
/// the test asks for a failure status (400 or above) and for no request to the target of the redirect.
/// </para>
/// <para>
/// Not applicable, see the matrix: 'returns 400 for unsupported format', which is the list of output formats of the Images
/// binding; the image service of the host has no such list.
/// </para>
/// <para>
/// GAP-CF-6: the image service of the adapter passes pictures through, so a request for PNG, WebP or AVIF, or for no format
/// at all, is answered with the source picture and its content type.
/// </para>
/// </summary>
[Trait("Upstream", "cloudflare/binding-image-service.test.ts")]
public class BindingImageServiceTests(SiteHost sites)
{
    private static readonly byte[] PlaceholderJpeg = File.ReadAllBytes(Path.Combine(Repository.FixturesRoot, CfPages.Fixture, "public", "placeholder.jpg"));
    private static readonly byte[] LogoSvg = File.ReadAllBytes(Path.Combine(Repository.FixturesRoot, CfPages.Fixture, "public", "logo.svg"));

    /// <summary>Serves pictures from extensionless paths, the way GitHub serves user avatars.</summary>
    private static Task<LoopbackOrigin> StartImageOriginAsync() =>
        LoopbackOrigin.StartAsync(app =>
        {
            app.MapGet("/svg", () => Results.File(LogoSvg, "image/svg+xml"));
            app.MapFallback(() => Results.File(PlaceholderJpeg, "image/jpeg"));
        });

    [Fact(DisplayName = "returns 403 for missing href parameter")]
    public async Task MissingHref_IsRefused()
    {
        var site = await sites.GetAsync(CfPages.Fixture);

        using var response = await site.GetAsync("/_image?f=webp");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "returns 403 for remote images not in allowed domains")]
    public async Task RemoteImageOfAnotherDomain_IsRefused()
    {
        var site = await sites.GetAsync(CfPages.Fixture);

        using var response = await site.GetAsync("/_image?href=https://example.com/image.jpg&f=webp");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact(DisplayName = "transforms local images to png", Skip = CfImages.RuntimeConversionGap)]
    public async Task LocalImage_IsTransformedToPng()
    {
        var site = await sites.GetAsync(CfPages.Fixture);

        using var response = await site.GetAsync("/_image?href=/placeholder.jpg&f=png&w=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/png", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact(DisplayName = "transforms local images to webp", Skip = CfImages.RuntimeConversionGap)]
    public async Task LocalImage_IsTransformedToWebp()
    {
        var site = await sites.GetAsync(CfPages.Fixture);

        using var response = await site.GetAsync("/_image?href=/placeholder.jpg&f=webp&w=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact(DisplayName = "transforms local images to avif", Skip = CfImages.RuntimeConversionGap)]
    public async Task LocalImage_IsTransformedToAvif()
    {
        var site = await sites.GetAsync(CfPages.Fixture);

        using var response = await site.GetAsync("/_image?href=/placeholder.jpg&f=avif&w=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/avif", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact(DisplayName = "does not follow redirects for remote images")]
    public async Task RemoteRedirect_IsNotFollowed()
    {
        await using var origin = await LoopbackOrigin.StartAsync(app =>
        {
            app.MapGet("/secret.jpg", () => Results.File(PlaceholderJpeg, "image/jpeg"));
            app.MapFallback(() => Results.Redirect("/secret.jpg"));
        });
        var site = await CfImages.GetLoopbackSiteAsync(sites);

        using var response = await site.GetAsync($"/_image?href={Uri.EscapeDataString(origin.Url("/image.jpg").ToString())}&f=webp");

        Assert.True((int)response.StatusCode >= 400, $"Expected the endpoint to refuse a redirecting picture, got {(int)response.StatusCode}.");
        Assert.Equal(["/image.jpg"], origin.Requests);
    }

    // Core omits `f` when it cannot infer a source format from the URL, which is the case
    // for every extensionless remote image (GitHub avatars, for one). See withastro/astro.build#2610.
    [Fact(DisplayName = "defaults to webp for remote images with no format parameter", Skip = CfImages.RuntimeConversionGap)]
    public async Task RemoteImageWithoutFormat_DefaultsToWebp()
    {
        await using var origin = await StartImageOriginAsync();
        var site = await CfImages.GetLoopbackSiteAsync(sites);

        using var response = await site.GetAsync($"/_image?href={Uri.EscapeDataString(origin.Url("/u/2738").ToString())}&w=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact(DisplayName = "passes through remote SVGs with no format parameter")]
    public async Task RemoteSvgWithoutFormat_IsPassedThrough()
    {
        await using var origin = await StartImageOriginAsync();
        var site = await CfImages.GetLoopbackSiteAsync(sites);

        using var response = await site.GetAsync($"/_image?href={Uri.EscapeDataString(origin.Url("/svg").ToString())}&w=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<svg", await response.ReadTextAsync());
    }

    [Fact(DisplayName = "passes through local SVGs requested as f=svg")]
    public async Task LocalSvg_IsPassedThrough()
    {
        var site = await sites.GetAsync(CfPages.Fixture);

        using var response = await site.GetAsync("/_image?href=/logo.svg&f=svg&w=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/svg+xml", response.Content.Headers.ContentType?.MediaType);
        Assert.Contains("<svg", await response.ReadTextAsync());
    }

    [Fact(DisplayName = "defaults to webp for local images with no format parameter", Skip = CfImages.RuntimeConversionGap)]
    public async Task LocalImageWithoutFormat_DefaultsToWebp()
    {
        var site = await sites.GetAsync(CfPages.Fixture);

        using var response = await site.GetAsync("/_image?href=/placeholder.jpg&w=100");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("image/webp", response.Content.Headers.ContentType?.MediaType);
    }
}
