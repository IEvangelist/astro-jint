using System.Net;
using System.Text;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/image-service-targets.test.ts</c> on the <c>core-image-ssr</c> fixture. Upstream builds it with the test
/// adapter and reads the output; here the build has the Jint adapter and the pages are requested from the host.
/// <list type="bullet">
/// <item>The two <c>it</c>s of <c>separate build and runtime image services</c> are not ported, and neither is the one of its dev-server
/// <c>describe</c> (it runs for two kinds of page): they
/// set <c>image.service</c> to <c>{ build, runtime }</c>, which the Astro of this repository (7.3.8) does not implement. Its config schema
/// drops the unknown keys, so such a site is built with the default service and no page gets the attributes the tests look for. The upstream
/// checkout the tests come from is ahead of that release (its <c>ImageServiceTargets</c> type is not in the published package). They are
/// listed in <c>docs/conformance/core.md</c>.</item>
/// <item>The one <c>it</c> of <c>build image service resolved by Vite</c> runs for two prerenderers upstream (the default one and a wrapping one
/// that an integration registers with <c>setPrerenderer</c>). The default one is ported and passes; the wrapping one is skipped
/// (<c>GAP-CORE-4</c>), so the matrix counts the <c>it</c> as a gap.</item>
/// <item>The <c>core-image-ssr</c> pages are the upstream ones. The Vite alias <c>~</c> of the two tests that resolve the service with Vite is the
/// <c>srcAlias</c> switch of the fixture config, because a JSON config cannot carry an absolute path (it is the same alias), and the
/// <c>wrappingPrerenderer</c> integration is the <c>wrappingPrerenderer</c> switch.</item>
/// <item>The tests read <c>client/prerender/index.html</c> from the output upstream; here they request <c>/prerender</c>, which the host serves
/// from that file. The generated image is a file of the output in both. The fresh asset cache upstream makes by deleting <c>cacheDir</c> is
/// the same here (the folder is deleted before the site is requested, so a build in this run generates the image with the service).</item>
/// </list>
/// </summary>
[Trait("Upstream", "core/image-service-targets.test.ts")]
public class ImageServiceTargetsTests(SiteHost sites)
{
    private const string Fixture = "core-image-ssr";

    private const string WrappingPrerendererGap =
        "GAP-CORE-4: a user integration that calls setPrerenderer() makes the build fail with this adapter, "
        + "see docs/conformance/core.md.";

    private static object AliasedService(string id, bool wrappingPrerenderer) => new
    {
        cacheDir = $"./node_modules/.astro-image-service-vite-{id}/",
        image = new { service = new { entrypoint = "~/aliased-image-service" }, domains = new[] { "avatars.githubusercontent.com" } },
        conformance = new { srcAlias = true, wrappingPrerenderer },
    };

    // A fresh asset cache, so the image is generated with the service on every run.
    private static void DeleteAssetCache(string id)
    {
        var cache = Path.Combine(Repository.FixturesRoot, Fixture, "node_modules", $".astro-image-service-vite-{id}");
        if (Directory.Exists(cache))
        {
            Directory.Delete(cache, recursive: true);
        }
    }

    private static async Task<AngleSharp.Dom.IElement> LocalImageAsync(ConformanceSite site, string page)
    {
        using var response = await site.GetAsync(page);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var image = (await response.ReadHtmlAsync()).QuerySelector("#local img");

        Assert.NotNull(image);
        return image;
    }

    private async Task AssertAliasedServiceGeneratedTheImageAsync(string id, bool wrappingPrerenderer)
    {
        DeleteAssetCache(id);
        var site = await sites.GetAsync(Fixture, AliasedService(id, wrappingPrerenderer));

        var image = await LocalImageAsync(site, "/prerender");

        Assert.Equal("/", image.GetAttribute("data-service-base"));
        var src = image.GetAttribute("src");
        Assert.NotNull(src);
        Assert.Matches(@"^/_astro/penguin2\..+\.webp$", src);
        var data = await File.ReadAllBytesAsync(Path.Combine(site.DistPath, "client", src[1..]));
        Assert.Equal("ALIASED_TRANSFORM", Encoding.UTF8.GetString(data, 0, 17));
    }

    [Fact(DisplayName = "astro:assets - build image service resolved by Vite (default prerenderer): generates images with an aliased TypeScript service that reads import.meta.env")]
    public Task DefaultPrerenderer_GeneratesImages_WithTheAliasedService() => AssertAliasedServiceGeneratedTheImageAsync("default-prerenderer", wrappingPrerenderer: false);

    [Fact(
        DisplayName = "astro:assets - build image service resolved by Vite (wrapping prerenderer): generates images with an aliased TypeScript service that reads import.meta.env",
        Skip = WrappingPrerendererGap)]
    public Task WrappingPrerenderer_GeneratesImages_WithTheAliasedService() => AssertAliasedServiceGeneratedTheImageAsync("wrapping-prerenderer", wrappingPrerenderer: true);

    // Not upstream: the engine renders with the aliased service too (the build step of the test above runs in Node)
    [Fact(DisplayName = "Not upstream: an on-demand page renders with the aliased TypeScript service inside the engine")]
    public async Task DefaultPrerenderer_OnDemandPage_RendersWithTheAliasedService()
    {
        var site = await sites.GetAsync(Fixture, AliasedService("default-prerenderer", wrappingPrerenderer: false));

        var image = await LocalImageAsync(site, "/");

        Assert.Equal("/", image.GetAttribute("data-service-base"));
        Assert.StartsWith("/_image?", image.GetAttribute("src"));
    }
}
