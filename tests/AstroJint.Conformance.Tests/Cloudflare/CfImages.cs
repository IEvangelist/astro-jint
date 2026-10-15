using System.Text;
using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// What the image tests of the Cloudflare adapter have in common: where the build wrote the pictures and the markup for them,
/// and the reasons of the tests that wait for an image service that converts pictures.
/// </summary>
internal static partial class CfImages
{
    public const string BuildConversionGap = "GAP-CF-5: the adapter replaces Sharp with a pass-through image service, so the build copies every picture with its format and size";

    public const string RuntimeConversionGap = "GAP-CF-6: /_image answers every request with the bytes and the content type of the source picture, it never converts or resizes";

    public const string SharpServiceGap = "GAP-CF-7: 'astro build' fails with MissingSharp when the image service of the site uses Sharp, the adapter bundles Sharp into the server output where its native addon cannot load";

    /// <summary>What the transform of <c>src/image-service.ts</c> puts in front of the bytes of a picture.</summary>
    public const string TransformMarker = "CUSTOM_TRANSFORM_RAN";

    /// <summary>The HTML that the build wrote for a prerendered route such as <c>images/prerendered</c>.</summary>
    public static Task<string> ReadPrerenderedAsync(ConformanceSite site, string route) =>
        File.ReadAllTextAsync(Path.Combine(site.DistPath, "client", route, "index.html"));

    /// <summary>A file of the client output, named by the URL it is served at.</summary>
    public static Task<byte[]> ReadClientFileAsync(ConformanceSite site, string url) =>
        File.ReadAllBytesAsync(Path.Combine(site.DistPath, "client", url.TrimStart('/')));

    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    /// <summary>The URLs in the <c>srcset</c> (or else the <c>src</c>) of the elements that match the selector.</summary>
    public static List<string> Srcs(IDocument document, string selector) =>
        [.. document.QuerySelectorAll(selector)
            .Select(element => element.GetAttribute("srcset") ?? element.GetAttribute("src"))
            .OfType<string>()
            .SelectMany(value => value.Split(',').Select(part => Whitespace().Split(part.Trim())[0]))];

    /// <summary>All JavaScript of the server output, which is where the image service of the site is bundled.</summary>
    public static string ReadServerBundle(ConformanceSite site) =>
        string.Join("\n", Directory.GetFiles(Path.Combine(site.DistPath, "server"), "*.mjs", SearchOption.AllDirectories).Select(File.ReadAllText));

    /// <summary>The shared site, with the host allowed to fetch from loopback origins, which is where the remote pictures of a test live.</summary>
    public static Task<ConformanceSite> GetLoopbackSiteAsync(SiteHost sites) =>
        sites.GetAsync(CfPages.Fixture, configure: options => options.FetchUrlFilter = url => url.IsLoopback, variant: "cf-loopback-fetch");

    public static void AssertWebp(byte[] data)
    {
        Assert.True(data.Length >= 12, $"Expected a WebP container, got {data.Length} bytes.");
        Assert.Equal("RIFF", Encoding.UTF8.GetString(data, 0, 4));
        Assert.Equal("WEBP", Encoding.UTF8.GetString(data, 8, 4));
    }

    public static void AssertSharpNotBundled(string serverBundle)
    {
        Assert.DoesNotContain("import(\"sharp\")", serverBundle);
        Assert.DoesNotContain("assets/services/sharp", serverBundle);
    }
}
