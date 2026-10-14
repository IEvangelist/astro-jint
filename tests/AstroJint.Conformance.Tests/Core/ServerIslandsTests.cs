using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using AngleSharp.Dom;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/server-islands.test.ts</c>, built from the <c>server-islands/ssr</c>, <c>server-islands/hybrid</c>
/// and <c>server-islands/custom-fetch</c> fixtures of Astro (<c>core-server-islands</c>, <c>core-server-islands-hybrid</c> and
/// <c>core-server-islands-custom-fetch</c>).
/// <list type="bullet">
/// <item>The content collection of <c>ssr</c> (<c>content.config.ts</c>, <c>slotted-island.mdx</c>, the page that renders it and the
/// <c>DefaultSlotWrapper</c> component) is the fixture <c>core-server-islands-content</c>. A build that renders a collection entry
/// does not run on this adapter (GAP-CORE-1), and it would take the other tests of <c>ssr</c> with it. Its test is skipped for that
/// gap. The one after it is not an upstream test: it is the same request with the SSR build split into chunks, and it passes, which
/// is how the gap is isolated.</item>
/// <item><c>app.render(new Request('http://example.com/...'))</c> becomes an HTTP request. A <c>POST</c> body is the string that
/// <c>JSON.stringify</c> gives upstream, so it travels as <c>text/plain</c> like the body of an upstream <c>Request</c>, and
/// <c>Origin: http://example.com</c> becomes the origin of the site, because the host builds the URL of a request from its
/// <c>Host</c> header.</item>
/// <item><c>encryptString()</c> of Astro (WebCrypto) becomes <see cref="AesGcm"/> with the same wire format: 12 random bytes as upper
/// case hex, then the Base64 of the cipher text followed by the tag, with <c>export:&lt;name&gt;</c>, <c>props:&lt;name&gt;</c> and
/// <c>slots:&lt;name&gt;</c> as authenticated data. The key is the <c>ASTRO_KEY</c> of the upstream tests. The upstream tests put it
/// in their own environment, but the build runs in another process here, so the fixture sets it in its Astro config.</item>
/// <item><c>loadFixture({ security: { checkOrigin: false } })</c> is the JSON config of the <c>SSR</c> tests.</item>
/// <item>The tests of the <c>dev</c> describe (five in <c>SSR</c>, <c>custom fetch handler</c> and the last one of <c>Hybrid mode</c>)
/// run against the production build, where the same pages and the same endpoint answer. The last one is about a bug of the dev
/// container with an adapter that sets no <c>buildOutput</c>; this adapter does set it, so the test shows that the endpoint of a
/// static site answers once it is built. The <c>custom fetch handler</c> fixture has a <c>src/fetch.ts</c>, which the production
/// entry of Astro installs as the fetch handler of the app.</item>
/// <item>The upstream test of <c>supports fragments with named slots</c> requests <c>/fragment</c> like the one before it, and
/// never requests the <c>/named-fragment</c> page of the fixture. Both pages are requested here.</item>
/// <item><c>fixture.readFile('/client/index.html')</c> reads the file of the build output.</item>
/// <item>The two tests of <c>with no adapter</c> check the error of Astro for a build or a dev server without an adapter. This
/// adapter, and so this host, is not part of them.</item>
/// </list>
/// </summary>
[Trait("Upstream", "core/server-islands.test.ts")]
public partial class ServerIslandsTests(SiteHost sites)
{
    private const string Fixture = "core-server-islands";
    private const string ContentFixture = "core-server-islands-content";
    private const string HybridFixture = "core-server-islands-hybrid";
    private const string CustomFetchFixture = "core-server-islands-custom-fetch";

    // Same key as the fixture sets in its Astro config.
    private const string AstroKey = "eKBaVEuI7YjfanEXHuJe/pwZKKt3LkAHeMxvTU7aR0M=";
    private const string InvalidEncryptedValue = "FC8337AF072BE5B1641501E1r8mLIhmIME1AV7UO9XmW9OLE";

    private static readonly object SsrConfig = new { security = new { checkOrigin = false } };

    private static readonly object CodeSplitSsrBuild = new
    {
        vite = new { environments = new { ssr = new { build = new { rolldownOptions = new { output = new { codeSplitting = true } } } } } },
    };

    [Fact(DisplayName = "Server islands: SSR: dev: HTML escapes scripts")]
    public async Task Page_WithScriptInProps_EscapesIt()
    {
        var site = await sites.GetAsync(Fixture, SsrConfig);

        using var response = await site.GetAsync("/");
        var html = await response.ReadTextAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain("</script><script>alert('xss')</script><!--", html);
    }

    [Fact(DisplayName = "Server islands: SSR: dev: island can set headers")]
    public async Task Island_CanSetResponseHeaders()
    {
        var site = await sites.GetAsync(Fixture, SsrConfig);

        // The upstream test sends no Origin, because the check of the origin is off in this configuration.
        using var response = await site.SendAsync(HttpMethod.Post, "/_server-islands/Island", IslandBody());

        Assert.Equal("true", Assert.Single(response.HeaderValues("X-Works")));
    }

    [Fact(DisplayName = "Server islands: SSR: dev: supports fragments")]
    public async Task Fragment_Island_HasNoEncryptedPropsInTheUrl()
    {
        var site = await sites.GetAsync(Fixture, SsrConfig);

        using var response = await site.GetAsync("/fragment");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(EncryptedPropsInQuery(await response.ReadTextAsync()));
    }

    [Fact(DisplayName = "Server islands: SSR: dev: supports fragments with named slots")]
    public async Task FragmentWithNamedSlot_Island_HasNoEncryptedPropsInTheUrl()
    {
        var site = await sites.GetAsync(Fixture, SsrConfig);

        foreach (var path in new[] { "/fragment", "/named-fragment" })
        {
            using var response = await site.GetAsync(path);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Empty(EncryptedPropsInQuery(await response.ReadTextAsync()));
        }
    }

    [Fact(DisplayName = "Server islands: SSR: dev: includes script from slotted component in island response")]
    public async Task Island_WithSlottedComponent_IncludesItsScript()
    {
        var site = await sites.GetAsync(Fixture, SsrConfig);

        using var page = await site.GetAsync("/slot-with-script");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var url = WrapperIslandUrl().Match(await page.ReadTextAsync());
        Assert.True(url.Success, "should have a server island fetch URL");

        using var island = await site.GetAsync(url.Groups[1].Value);
        var islandHtml = await island.ReadTextAsync();

        Assert.Equal(HttpStatusCode.OK, island.StatusCode);
        Assert.Contains("<script", islandHtml);
        Assert.Contains("data-increment", islandHtml);
    }

    [Fact(DisplayName = "Server islands: SSR: prod: omits the islands HTML")]
    public async Task Page_OmitsTheIslandHtml_AndHasTheIslandScript()
    {
        var site = await sites.GetAsync(Fixture, SsrConfig);

        using var response = await site.GetAsync("/");
        var document = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(document.QuerySelectorAll("h2#island"));
        Assert.Single(document.QuerySelectorAll("script[data-island-id]"));
    }

    [Fact(DisplayName = "Server islands: SSR: prod: island is not indexed")]
    public async Task Island_Response_IsNotIndexed()
    {
        var site = await sites.GetAsync(Fixture, SsrConfig);

        using var response = await PostIslandAsync(site, IslandBody());

        Assert.Equal("noindex", Assert.Single(response.HeaderValues("X-Robots-Tag")));
    }

    [Fact(DisplayName = "Server islands: SSR: prod: rejects invalid props")]
    public async Task Island_WithInvalidProps_IsRejected()
    {
        var site = await sites.GetAsync(Fixture, SsrConfig);

        using var response = await PostIslandAsync(site, IslandBody(encryptedProps: InvalidEncryptedValue));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "Server islands: SSR: prod: accepts encrypted slots via POST")]
    public async Task Island_WithEncryptedSlots_IsAccepted()
    {
        var site = await sites.GetAsync(Fixture, SsrConfig);
        var encryptedSlots = Encrypt("""{"content":"<p>Safe slot content</p>"}""", "slots:Island");

        using var response = await PostIslandAsync(site, IslandBody(encryptedSlots: encryptedSlots));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "Server islands: SSR: prod: rejects invalid encrypted slots via POST")]
    public async Task Island_WithInvalidEncryptedSlots_IsRejected()
    {
        var site = await sites.GetAsync(Fixture, SsrConfig);

        using var response = await PostIslandAsync(site, IslandBody(encryptedSlots: InvalidEncryptedValue));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact(DisplayName = "Server islands: SSR: prod: accepts encrypted slots with XSS payload via POST")]
    public async Task Island_WithEncryptedXssSlots_IsAccepted()
    {
        var site = await sites.GetAsync(Fixture, SsrConfig);
        var encryptedSlots = Encrypt("""{"xss":"<img src=x onerror=alert(0)>"}""", "slots:Island");

        using var response = await PostIslandAsync(site, IslandBody(encryptedSlots: encryptedSlots));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "Server islands: SSR: prod: supports mdx")]
    public async Task MdxPage_Island_HasNoEncryptedPropsInTheUrl()
    {
        var site = await sites.GetAsync(Fixture, SsrConfig);

        using var response = await site.GetAsync("/test/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Empty(EncryptedPropsInQuery(await response.ReadTextAsync()));
    }

    [Fact(
        DisplayName = "Server islands: SSR: prod: renders server island inside a slot in an MDX content collection entry",
        Skip = "GAP-CORE-1: the build of a site that renders a content collection entry keeps the manifest placeholder in entry.mjs, so every request fails")]
    public async Task ContentCollectionEntry_IslandInSlot_IsInsideTheWrapper()
    {
        var site = await sites.GetAsync(ContentFixture);

        await AssertIslandInsideWrapperAsync(site);
    }

    // Not an upstream test: the same request with the SSR build split into chunks, which is how GAP-CORE-1 is isolated.
    [Fact(DisplayName = "Server islands: SSR: prod: renders server island inside a slot in an MDX content collection entry (code-split build)")]
    public async Task ContentCollectionEntry_IslandInSlot_IsInsideTheWrapper_WhenTheBuildIsCodeSplit()
    {
        var site = await sites.GetAsync(ContentFixture, CodeSplitSsrBuild);

        await AssertIslandInsideWrapperAsync(site);
    }

    [Fact(DisplayName = "Server islands: custom fetch handler: renders framework components in an island requested after the page in dev")]
    public async Task CustomFetchHandler_Island_RendersFrameworkComponent()
    {
        var site = await sites.GetAsync(CustomFetchFixture);

        using var page = await site.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var url = IslandUrl().Match(await page.ReadTextAsync());
        Assert.True(url.Success, "should have a server island fetch URL");

        using var island = await site.GetAsync(url.Groups[1].Value);
        var islandHtml = await island.ReadTextAsync();

        Assert.Equal(HttpStatusCode.OK, island.StatusCode);
        Assert.Contains("Rendered by Svelte", islandHtml);
    }

    [Fact(DisplayName = "Server islands: Hybrid mode: build: Omits the island HTML from the static HTML")]
    public async Task HybridBuild_StaticHtml_OmitsTheIslandHtml()
    {
        var document = await ReadHybridIndexAsync();

        Assert.Empty(document.QuerySelectorAll("h2#island"));
        Assert.Equal(2, document.QuerySelectorAll("script[data-island-id]").Length);
    }

    [Fact(DisplayName = "Server islands: Hybrid mode: build: includes the server island runtime script once")]
    public async Task HybridBuild_StaticHtml_IncludesTheRuntimeScriptOnce()
    {
        var document = await ReadHybridIndexAsync();

        var runtimeScripts = document.QuerySelectorAll("script")
            .Where(script => script.InnerHtml.Trim().StartsWith("async function replaceServerIsland", StringComparison.Ordinal));

        Assert.Single(runtimeScripts);
    }

    [Fact(DisplayName = "Server islands: Hybrid mode: can fetch the server island endpoint in dev with adapter that does not set buildOutput")]
    public async Task HybridSite_IslandEndpoint_Answers()
    {
        var site = await sites.GetAsync(HybridFixture);

        using var page = await site.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var url = HybridIslandUrl().Match(await page.ReadTextAsync());
        Assert.True(url.Success, "should have a server island fetch URL");

        using var island = await site.GetAsync(url.Groups[1].Value);

        // Not the GetStaticPathsRequired error.
        Assert.Equal(HttpStatusCode.OK, island.StatusCode);
    }

    private static async Task AssertIslandInsideWrapperAsync(ConformanceSite site)
    {
        using var response = await site.GetAsync("/content-collection-island-slot/");
        var document = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Single(document.QuerySelectorAll("#default-wrapper"));
        Assert.Single(document.QuerySelectorAll("#default-wrapper script[data-island-id]"));
    }

    private async Task<IDocument> ReadHybridIndexAsync()
    {
        var site = await sites.GetAsync(HybridFixture);
        return ConformanceSite.ParseHtml(await File.ReadAllTextAsync(Path.Combine(site.DistPath, "client", "index.html")));
    }

    private static Task<HttpResponseMessage> PostIslandAsync(ConformanceSite site, HttpContent body) =>
        site.SendAsync(HttpMethod.Post, "/_server-islands/Island", body, ("Origin", site.BaseAddress.GetLeftPart(UriPartial.Authority)));

    private static StringContent IslandBody(string? encryptedProps = null, string encryptedSlots = "") =>
        new(
            JsonSerializer.Serialize(new
            {
                encryptedComponentExport = Encrypt("default", "export:Island"),
                encryptedProps = encryptedProps ?? Encrypt("{}", "props:Island"),
                encryptedSlots,
            }),
            Encoding.UTF8);

    private static string EncryptedPropsInQuery(string html)
    {
        var match = IslandPropsParameter().Match(html);
        Assert.True(match.Success, "should include props in the query string");
        return match.Groups[1].Value;
    }

    private static string Encrypt(string plainText, string additionalData)
    {
        var iv = RandomNumberGenerator.GetBytes(12);
        var data = Encoding.UTF8.GetBytes(plainText);
        var cipherText = new byte[data.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(Convert.FromBase64String(AstroKey), tag.Length);
        aes.Encrypt(iv, data, cipherText, tag, Encoding.UTF8.GetBytes(additionalData));

        return Convert.ToHexString(iv) + Convert.ToBase64String([.. cipherText, .. tag]);
    }

    [GeneratedRegex("""fetch\(["']\/_server-islands\/Island\?[^"']*p=([^&"']*)""")]
    private static partial Regex IslandPropsParameter();

    [GeneratedRegex("""fetch\(["'](\/_server-islands\/Wrapper\?[^"']+)["']""")]
    private static partial Regex WrapperIslandUrl();

    [GeneratedRegex("""fetch\(["'](\/_server-islands\/Island\?[^"']+)["']""")]
    private static partial Regex IslandUrl();

    [GeneratedRegex("""fetch\(["'](\/_server-islands\/Island[^"']*)""")]
    private static partial Regex HybridIslandUrl();
}
