using System.Net;
using System.Text.RegularExpressions;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/ssr-script.test.ts</c>. Upstream renders the pages with <c>app.render()</c> of the test adapter and
/// globs the build output; here the pages and scripts are requested from the host and the globs run on <c>dist</c>. Every build
/// variant upstream passes to <c>loadFixture()</c> is a JSON config variant of the one <c>core-ssr-script</c> fixture. The
/// <c>script has correct path</c> tests also request the script they find, and for an <c>assetsPrefix</c> (a CDN upstream) they request the
/// path of the URL from the host, where the same file lives. The Jint adapter has no <c>client.assetQueryParams</c> option, which the
/// <c>with assetQueryParams</c> test sets through the test adapter's <c>extendAdapter</c>; the fixture config decorates the adapter's
/// <c>setAdapter()</c> call to carry it, and the test checks the client files of that build.
/// </summary>
[Trait("Upstream", "core/ssr-script.test.ts")]
public class SsrScriptTests(SiteHost sites)
{
    private const string Fixture = "core-ssr-script";

    private static readonly object NoInlining = new { build = new { assetsInlineLimit = 0 } };

    private static readonly object CustomFileNames = new
    {
        build = new { assetsInlineLimit = 0 },
        environments = new
        {
            client = new
            {
                build = new
                {
                    rollupOptions = new
                    {
                        output = new
                        {
                            entryFileNames = "assets/entry.[hash].mjs",
                            chunkFileNames = "assets/chunks/chunk.[hash].mjs",
                            assetFileNames = "assets/asset.[hash][extname]",
                        },
                    },
                },
            },
        },
    };

    private static List<string> ScriptFiles(ConformanceSite site, string folder) =>
        Directory.EnumerateFiles(Path.Combine(site.DistPath, folder), "*", SearchOption.AllDirectories)
            .Where(file => file.EndsWith(".js", StringComparison.Ordinal) || file.EndsWith(".mjs", StringComparison.Ordinal))
            .Select(file => Path.GetRelativePath(site.DistPath, file).Replace('\\', '/'))
            .ToList();

    private static async Task<string> ScriptSourceAsync(ConformanceSite site, string page)
    {
        using var response = await site.GetAsync(page);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var source = (await response.ReadHtmlAsync()).QuerySelector("script")?.GetAttribute("src");
        Assert.NotNull(source);
        return source;
    }

    private static async Task AssertScriptIsServedAsync(ConformanceSite site, string source)
    {
        var path = new Uri(new Uri("http://localhost/"), source).AbsolutePath;

        using var response = await site.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/javascript", response.Content.Headers.ContentType?.MediaType);
        Assert.NotEmpty(await response.ReadTextAsync());
    }

    [Fact(DisplayName = "scripts get included")]
    public async Task InlineScript_WithoutBase_IsIncluded()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var scripts = (await response.ReadHtmlAsync()).QuerySelectorAll("script");
        Assert.Single(scripts);
    }

    [Fact(DisplayName = "server output filenames do not contain unsafe characters")]
    public async Task ServerOutput_HasNoUnsafeFileNames()
    {
        var site = await sites.GetAsync(Fixture);

        var files = ScriptFiles(site, "server");

        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            Assert.DoesNotMatch("[!~#{}<>]", file);
        }
    }

    [Fact(DisplayName = "client output filenames do not contain hash placeholders or unsafe characters")]
    public async Task ClientOutput_WithAssetQueryParams_HasNoUnsafeFileNames()
    {
        var site = await sites.GetAsync(Fixture, new { conformance = new { assetQueryParams = new { dpl = "test123" } } });

        var files = ScriptFiles(site, "client");

        Assert.NotEmpty(files);
        foreach (var file in files)
        {
            Assert.DoesNotMatch("[!~{}]", file);
        }

        // The query parameters end up in the dynamic import of the page script, and the host serves the chunk regardless of them.
        using var script = await site.GetAsync(await ScriptSourceAsync(site, "/dynamic"));
        var chunk = Regex.Match(await script.ReadTextAsync(), @"import\(`\./(?<file>confetti\.[\w-]+\.js\?dpl=test123)`\)");
        Assert.True(chunk.Success, "The page script does not import the chunk with the asset query parameters.");
        using var chunkResponse = await site.GetAsync($"/_astro/{chunk.Groups["file"].Value}");
        Assert.Equal(HttpStatusCode.OK, chunkResponse.StatusCode);
        Assert.Contains("confetti!", await chunkResponse.ReadTextAsync());
    }

    [Fact(DisplayName = "Inlined scripts get included without base path in the script")]
    public async Task InlineScript_WithBase_HasNoBasePathInTheScript()
    {
        var site = await sites.GetAsync(Fixture, new { @base = "/hello" });

        using var response = await site.GetAsync("/hello/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("console.log(`hello world`);", (await response.ReadHtmlAsync()).QuerySelector("script")?.TextContent);
    }

    [Fact(DisplayName = "without base path: script has correct path")]
    public async Task ExternalScript_WithoutBase_HasCorrectPath()
    {
        var site = await sites.GetAsync(Fixture, new { vite = NoInlining });

        var source = await ScriptSourceAsync(site, "/");

        Assert.Matches(@"^/_astro/.*\.js$", source);
        await AssertScriptIsServedAsync(site, source);
    }

    [Fact(DisplayName = "with base path: script has correct path")]
    public async Task ExternalScript_WithBase_HasCorrectPath()
    {
        var site = await sites.GetAsync(Fixture, new { vite = NoInlining, @base = "/hello" });

        var source = await ScriptSourceAsync(site, "/hello/");

        Assert.Matches(@"^/hello/_astro/.*\.js$", source);
        await AssertScriptIsServedAsync(site, source);
    }

    [Fact(DisplayName = "with assetsPrefix: script has correct path")]
    public async Task ExternalScript_WithAssetsPrefix_HasCorrectPath()
    {
        var site = await sites.GetAsync(Fixture, new { build = new { assetsPrefix = "https://cdn.example.com" }, vite = NoInlining });

        var source = await ScriptSourceAsync(site, "/");

        Assert.Matches(@"^https://cdn\.example\.com/_astro/.*\.js$", source);
        await AssertScriptIsServedAsync(site, source);
    }

    [Fact(DisplayName = "with custom rollup output file names: script has correct path")]
    public async Task ExternalScript_WithCustomFileNames_HasCorrectPath()
    {
        var site = await sites.GetAsync(Fixture, new { vite = CustomFileNames });

        var source = await ScriptSourceAsync(site, "/");

        Assert.Matches(@"^/assets/entry\..{8}\.mjs$", source);
        await AssertScriptIsServedAsync(site, source);
    }

    [Fact(DisplayName = "with custom rollup output file names and base: script has correct path")]
    public async Task ExternalScript_WithCustomFileNamesAndBase_HasCorrectPath()
    {
        var site = await sites.GetAsync(Fixture, new { vite = CustomFileNames, @base = "/hello" });

        var source = await ScriptSourceAsync(site, "/hello/");

        Assert.Matches(@"^/hello/assets/entry\..{8}\.mjs$", source);
        await AssertScriptIsServedAsync(site, source);
    }

    [Fact(DisplayName = "with custom rollup output file names and assetsPrefix: script has correct path")]
    public async Task ExternalScript_WithCustomFileNamesAndAssetsPrefix_HasCorrectPath()
    {
        var site = await sites.GetAsync(
            Fixture,
            new { build = new { assetsPrefix = "https://cdn.example.com" }, vite = CustomFileNames });

        var source = await ScriptSourceAsync(site, "/");

        Assert.Matches(@"^https://cdn\.example\.com/assets/entry\..{8}\.mjs$", source);
        await AssertScriptIsServedAsync(site, source);
    }
}
