using System.Globalization;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace AstroJint.Conformance.CoreBuild;

/// <summary>
/// Port of <c>packages/astro/test/fonts.test.ts</c>: the <c>&lt;Font&gt;</c> component (styles, preload links, preload filters),
/// <c>fontData</c> and <c>experimental_getFontFileURL()</c> of the experimental <c>fonts</c> config. Upstream has 20 tests on the
/// <c>fonts</c> fixture in three groups: the dev server (8, not ported, see the matrix), <c>astro build</c> with static pages
/// that it reads from <c>dist</c>, and <c>output: 'server'</c> with <c>app.render()</c> of a test adapter. Here the build group
/// requests the prerendered pages from the host and the ssr group requests pages that Jint renders.
/// <para>
/// Differences from upstream: the font family of <c>cb-fonts</c> is <c>fontProviders.local()</c> with two font files of the
/// fixture (weights 400 and 500) instead of Poppins from <c>fontProviders.fontsource()</c>, so that the build needs no network;
/// there are two files because Astro preloads a font file once, whatever number of faces use it. Every font URL that a page gets
/// (the <c>@font-face</c> sources, the preload links, the URLs of <c>fontData</c>) is also requested from the host, which has to
/// serve it with status 200, the <c>font/woff2</c> content type and the immutable cache header. "Can filter preloads" also
/// checks that exactly one of the two preloads is left, "Exposes buffer" that the length is the size of the font file that the
/// host serves (upstream: not zero), and "Respects config to build links: works" requests the files instead of listing
/// <c>dist/_custom/fonts</c> (and checks that they are in <c>client/_custom/fonts</c>).
/// </para>
/// <para>
/// Adapted: in the ssr group "Exposes buffer" the page fetches the font file from the host that runs it (upstream's test adapter
/// has no limit on <c>fetch</c> and the request goes to <c>example.com</c>). The host rejects every JavaScript <c>fetch</c>
/// unless <c>AstroOptions.FetchUrlFilter</c> allows it, so this group runs with a host that allows loopback addresses.
/// </para>
/// </summary>
[Trait("Upstream", "core/fonts.test.ts")]
public partial class FontsTests(SiteHost sites)
{
    private const string Fixture = "cb-fonts";

    private static readonly object PrefixedConfig = new
    {
        @base = "/my-base",
        build = new { assets = "_custom", assetsPrefix = "https://cdn.example.com" },
    };

    [GeneratedRegex("""url\("([^"]+)"\)""")]
    private static partial Regex FontFaceUrl { get; }

    private Task<ConformanceSite> SiteAsync(string mode) => mode switch
    {
        "build" => sites.GetAsync(Fixture),
        "ssr" => sites.GetAsync(
            Fixture,
            new { output = "server" },
            configure: options => options.FetchUrlFilter = url => url.IsLoopback,
            variant: "loopback-fetch"),
        _ => throw new ArgumentOutOfRangeException(nameof(mode), mode, null),
    };

    private static async Task<(string Html, IDocument Document)> PageAsync(ConformanceSite site, string path)
    {
        using var response = await site.GetAsync(path);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path} answered {(int)response.StatusCode}.");

        var html = await response.ReadTextAsync();
        return (html, ConformanceSite.ParseHtml(html));
    }

    private static IReadOnlyList<IElement> PreloadLinks(IDocument document) =>
        [.. document.QuerySelectorAll("link[rel=preload][type='font/woff2']")];

    /// <summary>The URL of the first source of every face in the <c>fontData</c> that the page at <paramref name="path"/> prints.</summary>
    private static async Task<IReadOnlyList<string>> FontDataUrlsAsync(ConformanceSite site, string path)
    {
        var (_, document) = await PageAsync(site, path);
        using var data = JsonDocument.Parse(document.QuerySelector("#data")!.TextContent);
        var faces = data.RootElement.GetProperty("--font-test");

        Assert.Equal(JsonValueKind.Array, faces.ValueKind);
        Assert.True(faces.GetArrayLength() > 0);
        return [.. faces.EnumerateArray().Select(face => face.GetProperty("src")[0].GetProperty("url").GetString()!)];
    }

    /// <summary>
    /// The page that fetches the font file of the first face with <c>experimental_getFontFileURL()</c> and prints the length of
    /// what it got, which has to be the size of the file that the host serves under that URL.
    /// </summary>
    private static async Task AssertFontBufferAsync(ConformanceSite site, string basePath = "", string assetsDir = "_astro")
    {
        var (_, document) = await PageAsync(site, $"{basePath}/get-font-buffer");
        var length = document.QuerySelector("#length")?.TextContent;

        Assert.False(string.IsNullOrEmpty(length));
        Assert.NotEqual("0", length);

        var url = (await FontDataUrlsAsync(site, $"{basePath}/font-data"))[0];
        using var font = await site.GetAsync(PageAssets.HostTarget(url, basePath, assetsDir));
        Assert.Equal(HttpStatusCode.OK, font.StatusCode);
        Assert.Equal((await font.Content.ReadAsByteArrayAsync()).Length.ToString(CultureInfo.InvariantCulture), length);
    }

    [Theory(DisplayName = "Includes styles")]
    [InlineData("build")]
    [InlineData("ssr")]
    public async Task Page_IncludesTheFontStyles(string mode)
    {
        var site = await SiteAsync(mode);

        var (html, document) = await PageAsync(site, "/");

        Assert.Contains("<style>", html);
        Assert.Empty(PreloadLinks(document));

        var fontFaceUrls = FontFaceUrl.Matches(document.QuerySelector("style")!.TextContent).Select(match => match.Groups[1].Value).ToList();
        Assert.NotEmpty(fontFaceUrls);
        foreach (var url in fontFaceUrls)
        {
            await PageAssets.AssertServedAsync(site, url);
        }
    }

    [Theory(DisplayName = "Includes links when preloading")]
    [InlineData("build")]
    [InlineData("ssr")]
    public async Task Preload_IncludesTheLinks(string mode)
    {
        var site = await SiteAsync(mode);

        var (_, document) = await PageAsync(site, "/preload");

        var links = PreloadLinks(document);
        Assert.NotEmpty(links);
        foreach (var link in links)
        {
            var href = link.GetAttribute("href")!;

            Assert.StartsWith("/_astro/fonts/", href);
            await PageAssets.AssertServedAsync(site, href);
        }
    }

    [Theory(DisplayName = "Can filter preloads")]
    [InlineData("build")]
    [InlineData("ssr")]
    public async Task Preload_CanBeFiltered(string mode)
    {
        var site = await SiteAsync(mode);

        var (_, all) = await PageAsync(site, "/preload");
        var (_, granular) = await PageAsync(site, "/granular-preload");

        var allPreloads = PreloadLinks(all);
        var filteredPreloads = PreloadLinks(granular);
        Assert.True(filteredPreloads.Count < allPreloads.Count, $"{filteredPreloads.Count} filtered preloads, {allPreloads.Count} in all.");
        Assert.Equal(2, allPreloads.Count);
        Assert.Equal(allPreloads[0].GetAttribute("href"), Assert.Single(filteredPreloads).GetAttribute("href"));
    }

    [Theory(DisplayName = "Exposes fontData")]
    [InlineData("build")]
    [InlineData("ssr")]
    public async Task FontData_IsExposed(string mode)
    {
        var site = await SiteAsync(mode);

        var urls = await FontDataUrlsAsync(site, "/font-data");

        foreach (var url in urls)
        {
            Assert.StartsWith("/_astro/fonts/", url);
            await PageAssets.AssertServedAsync(site, url);
        }
    }

    [Theory(DisplayName = "Exposes buffer from experimental_getFontFileURL()")]
    [InlineData("build")]
    [InlineData("ssr")]
    public async Task FontBuffer_IsExposed(string mode)
    {
        var site = await SiteAsync(mode);

        await AssertFontBufferAsync(site);
    }

    [Fact(DisplayName = "Respects config to build links: works")]
    public async Task Build_LinksFollowTheBaseTheAssetsDirAndThePrefix()
    {
        var site = await sites.GetAsync(Fixture, PrefixedConfig);

        var (_, document) = await PageAsync(site, "/my-base/preload");

        var links = PreloadLinks(document);
        Assert.NotEmpty(links);
        foreach (var link in links)
        {
            var href = link.GetAttribute("href")!;

            Assert.StartsWith("https://cdn.example.com/my-base/_custom/fonts/", href);
            await PageAssets.AssertServedAsync(site, href, "/my-base", "_custom");
            Assert.True(site.PathExists($"client/_custom/fonts/{Path.GetFileName(href)}"), $"{href} is not in client/_custom/fonts.");
        }
    }

    [Fact(DisplayName = "Respects config to build links: Exposes buffer from experimental_getFontFileURL()")]
    public async Task Build_FontBufferIsExposedWithTheConfigToBuildLinks()
    {
        var site = await sites.GetAsync(Fixture, PrefixedConfig);

        await AssertFontBufferAsync(site, "/my-base", "_custom");
    }
}
