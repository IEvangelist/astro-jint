using System.Text.Json;
using static AstroJint.Conformance.CoreUnits.AppServerIslands;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/server-islands/server-islands-render.test.ts</c>, which builds a stub <c>SSRResult</c> and
/// drives <c>containsServerDirective()</c>, <c>renderServerIslandRuntime()</c> and <c>ServerIslandComponent</c> directly. All 26
/// tests are adapted, in one of two ways. Where a test only needs what a page renders for an island (the script and its URL, the
/// marker, the fallback, the slots, the encrypted values, the hashes of the CSP), it requests the page of the
/// <c>app-server-islands</c> fixture that has that island, asserts the same thing on its HTML, decrypts the values of the script
/// with the key of the fixture and, where that says more, requests the island the way the browser does and asserts on the
/// fragment. Where it needs a stub that no page can build (a hostile <c>base</c>, a component that the map does not know, the
/// pending slot evaluations of a propagating route, one component initialised twice, the bare functions), the probe
/// <c>/probe/server-islands</c> (<c>src/lib/probes/server-islands.ts</c>) runs the very same calls with the very same stub inside
/// the host and answers with what the test asserts on; the assertions are here. The three builds of the fixture have the default
/// <c>trailingSlash: 'never'</c>, a <c>base</c> with <c>trailingSlash: 'always'</c>, and the CSP. <c>astro</c> does not export
/// <c>core/encryption.js</c>, so the probe imports it by its path in the installed package.
/// </summary>
[Trait("Upstream", "core-units/server-islands/server-islands-render.test.ts")]
public class ServerIslandsRenderTests(SiteHost sites)
{
    private const string ProbeName = "server-islands";

    // `p=` with nothing after it before the next parameter or the end of the string of the URL: the GET of an island without props.
    private const string EmptyPropsPattern = "[?&]p=(?:&|\")";

    private async Task<Probe> RunProbeAsync() => await (await sites.GetServerIslandsSiteAsync()).ProbeAsync(ProbeName);

    private static async Task<(string Html, IslandScript[] Islands)> OpenAsync(ConformanceSite site, string path)
    {
        var page = await site.SendRawAsync(path);
        Assert.Equal(200, page.StatusCode);
        return (page.Text, Islands(page.Text));
    }

    private static async Task<string> FetchFragmentAsync(ConformanceSite site, IslandScript island)
    {
        using var response = await island.FetchAsync(site);
        Assert.Equal(200, (int)response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    // The sources of the script-src directive of the policy that the response carries.
    private static string[] ScriptSources(RawResponse page)
    {
        var policy = page.Header("Content-Security-Policy");
        Assert.NotNull(policy);

        var directive = policy.Split(';').Select(part => part.Trim()).First(part => part.StartsWith("script-src ", StringComparison.Ordinal));
        return directive.Split(' ', StringSplitOptions.RemoveEmptyEntries)[1..];
    }

    private static int CountOf(string text, string value)
    {
        var count = 0;
        for (var index = text.IndexOf(value, StringComparison.Ordinal); index >= 0; index = text.IndexOf(value, index + value.Length, StringComparison.Ordinal))
        {
            count++;
        }

        return count;
    }

    // #region containsServerDirective

    [Fact(DisplayName = "returns true when server:component-directive is present")]
    public async Task ContainsServerDirective_IsTrueWhenTheDirectiveIsPresent()
    {
        var probe = await RunProbeAsync();

        Assert.True(probe.Bool("returns true when server:component-directive is present"));
    }

    [Fact(DisplayName = "returns false when server:component-directive is absent")]
    public async Task ContainsServerDirective_IsFalseWhenTheDirectiveIsAbsent()
    {
        var probe = await RunProbeAsync();

        Assert.False(probe.Bool("returns false when server:component-directive is absent"));
    }

    [Fact(DisplayName = "returns false for an empty props object")]
    public async Task ContainsServerDirective_IsFalseForEmptyProps()
    {
        var probe = await RunProbeAsync();

        Assert.False(probe.Bool("returns false for an empty props object"));
    }

    // #endregion

    // #region renderServerIslandRuntime

    [Fact(DisplayName = "returns a <script> tag containing the replaceServerIsland function")]
    public async Task RenderServerIslandRuntime_ReturnsAScriptTagWithTheReplacer()
    {
        var probe = await RunProbeAsync();

        var output = probe.Str("returns a <script> tag containing the replaceServerIsland function");

        Assert.Contains("<script>", output, StringComparison.Ordinal);
        Assert.Contains("</script>", output, StringComparison.Ordinal);
        Assert.Contains("replaceServerIsland", output, StringComparison.Ordinal);

        // The page of an island has that script, once, before the script of the island that calls it.
        var site = await sites.GetServerIslandsSiteAsync();
        var (html, islands) = await OpenAsync(site, "/");
        Assert.Contains(output, html, StringComparison.Ordinal);
        Assert.True(html.IndexOf(output, StringComparison.Ordinal) < html.IndexOf("data-island-id", StringComparison.Ordinal));
        Assert.Single(islands);
    }

    [Fact(DisplayName = "escapes </script> sequences inside the runtime script")]
    public async Task RenderServerIslandRuntime_EscapesScriptEndTags()
    {
        var probe = await RunProbeAsync();

        var output = probe.Str("escapes </script> sequences inside the runtime script");

        // The content between the script tags must not contain an unescaped </script (only the closing tag at the very end is allowed).
        Assert.StartsWith("<script>", output, StringComparison.Ordinal);
        Assert.EndsWith("</script>", output, StringComparison.Ordinal);
        var inner = output["<script>".Length..^"</script>".Length];
        Assert.DoesNotContain("</script", inner, StringComparison.Ordinal);

        // The tags of the page match: nothing in the scripts that it renders closes one of them early.
        var site = await sites.GetServerIslandsSiteAsync();
        var (html, _) = await OpenAsync(site, "/twice");
        Assert.Equal(CountOf(html, "<script"), CountOf(html, "</script>"));
    }

    // #endregion

    // #region ServerIslandComponent: getIslandContent()

    [Fact(DisplayName = "omits props from the URL when no user props are provided")]
    public async Task GetIslandContent_OmitsPropsFromTheUrlWhenThereAreNone()
    {
        var site = await sites.GetServerIslandsSiteAsync();

        var (_, islands) = await OpenAsync(site, "/");

        var island = Assert.Single(islands);
        // Matches `p=` followed by the next parameter (`?e=...&p=&s=`, a GET): the encrypted props are empty.
        Assert.Matches(EmptyPropsPattern, island.Script);
        Assert.Equal("", island.Props);
    }

    [Fact(DisplayName = "includes encrypted props in the URL when user props are provided")]
    public async Task GetIslandContent_IncludesEncryptedPropsInTheUrl()
    {
        var site = await sites.GetServerIslandsSiteAsync();

        var (_, islands) = await OpenAsync(site, "/props");

        var island = Assert.Single(islands);
        Assert.DoesNotMatch(EmptyPropsPattern, island.Script);
        Assert.NotEqual("", island.Props);
        Assert.Equal("""{"message":"hello","userProp":"visible"}""", Decrypt(island.Props, "props:Island"));
    }

    [Fact(DisplayName = "produces different ciphertexts on each call (IV randomness)")]
    public async Task GetIslandContent_ProducesDifferentCiphertextsOnEachCall()
    {
        var site = await sites.GetServerIslandsSiteAsync();

        // Two islands with identical props on one page.
        var (_, islands) = await OpenAsync(site, "/twice");

        Assert.Equal(2, islands.Length);
        var (first, second) = (islands[0], islands[1]);
        Assert.NotEqual(first.Script, second.Script);
        Assert.NotEqual(first.Url, second.Url);
        Assert.NotEqual(first.Export, second.Export);
        Assert.NotEqual(first.Props, second.Props);

        // The values differ because of the IV of each call, not because of what they encrypt.
        Assert.Equal("""{"value":"test"}""", Decrypt(first.Props, "props:Island"));
        Assert.Equal("""{"value":"test"}""", Decrypt(second.Props, "props:Island"));
        Assert.Equal(Decrypt(first.Export, "export:Island"), Decrypt(second.Export, "export:Island"));
    }

    [Fact(DisplayName = "uses a GET request for small payloads (under 2048 chars)")]
    public async Task GetIslandContent_UsesGetForSmallPayloads()
    {
        var site = await sites.GetServerIslandsSiteAsync();

        var (_, islands) = await OpenAsync(site, "/");

        var island = Assert.Single(islands);
        // A small payload uses a plain fetch() GET call (no method: 'POST').
        Assert.Equal("GET", island.Method);
        Assert.DoesNotContain("method: 'POST'", island.Script, StringComparison.Ordinal);
        Assert.Contains("fetch(\"", island.Script, StringComparison.Ordinal);

        // And the island answers that GET.
        Assert.Contains("id=\"island\"", await FetchFragmentAsync(site, island), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "uses a POST request for large payloads (over 2048 chars)")]
    public async Task GetIslandContent_UsesPostForLargePayloads()
    {
        var site = await sites.GetServerIslandsSiteAsync();

        // The island of the page has a prop of 2048 characters, more than a URL of 2048 characters can carry once encrypted.
        var (_, islands) = await OpenAsync(site, "/large");

        var island = Assert.Single(islands);
        Assert.Equal("POST", island.Method);
        Assert.Contains("method: 'POST'", island.Script, StringComparison.Ordinal);
        Assert.Equal("/_server-islands/Island", island.Url);

        // The encrypted values travel in the body, and the island gets the whole prop.
        var fragment = ConformanceSite.ParseHtml(await FetchFragmentAsync(site, island));
        Assert.Equal($$"""{"data":"{{new string('x', 2048)}}"}""", fragment.QuerySelector("#island")?.GetAttribute("data-props"));
    }

    [Fact(DisplayName = "builds the island URL from base + componentId")]
    public async Task GetIslandContent_BuildsTheUrlFromTheBaseAndTheComponentId()
    {
        var site = await sites.GetServerIslandsBaseSiteAsync();

        var (_, islands) = await OpenAsync(site, "/app/");

        var island = Assert.Single(islands);
        Assert.Contains("/app/_server-islands/Island", island.Script, StringComparison.Ordinal);
        Assert.StartsWith("/app/_server-islands/Island", island.Url, StringComparison.Ordinal);

        // The endpoint is where the URL says.
        Assert.Contains("id=\"island\"", await FetchFragmentAsync(site, island), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "escapes the island URL for script and attribute contexts")]
    public async Task GetIslandContent_EscapesTheUrlForScriptAndAttributeContexts()
    {
        var probe = await RunProbeAsync();

        // The stub has the base `/"</script><script>test</script>`, which a build cannot have: the probe renders it.
        var result = probe["escapes the island URL for script and attribute contexts"];
        var content = result.Str("content");
        var preload = result.Str("preload");

        Assert.DoesNotContain("</script>", content, StringComparison.Ordinal);
        Assert.Contains("\\u003c/script>", content, StringComparison.Ordinal);
        Assert.Contains("&quot;", preload, StringComparison.Ordinal);
        Assert.DoesNotContain("href=\"/\"", preload, StringComparison.Ordinal);

        // On a page the URL has `&`: the script has it as is and the attribute of the preload has `&amp;`, for the same URL.
        var site = await sites.GetServerIslandsSiteAsync();
        var (html, islands) = await OpenAsync(site, "/");
        var island = Assert.Single(islands);
        Assert.Contains("&amp;p=&amp;s=", html, StringComparison.Ordinal);
        Assert.Equal(island.Url, ConformanceSite.ParseHtml(html).QuerySelector("link[rel=preload][as=fetch]")?.GetAttribute("href"));
    }

    [Fact(DisplayName = "appends a trailing slash when trailingSlash is \"always\"")]
    public async Task GetIslandContent_AppendsATrailingSlashWhenItIsAlways()
    {
        var site = await sites.GetServerIslandsBaseSiteAsync();

        var (_, islands) = await OpenAsync(site, "/app/");

        var island = Assert.Single(islands);
        Assert.Contains("/_server-islands/Island/", island.Script, StringComparison.Ordinal);
        Assert.Equal("/app/_server-islands/Island/", island.Path);
    }

    [Fact(DisplayName = "does not append a trailing slash when trailingSlash is \"never\"")]
    public async Task GetIslandContent_DoesNotAppendATrailingSlashWhenItIsNever()
    {
        var site = await sites.GetServerIslandsSiteAsync();

        var (_, islands) = await OpenAsync(site, "/");

        var island = Assert.Single(islands);
        Assert.DoesNotContain("/_server-islands/Island/", island.Script, StringComparison.Ordinal);
        Assert.Equal("/_server-islands/Island", island.Path);
    }

    [Fact(DisplayName = "the encrypted componentExport round-trips correctly")]
    public async Task GetIslandContent_EncryptsTheComponentExport()
    {
        var site = await sites.GetServerIslandsSiteAsync();

        var (_, islands) = await OpenAsync(site, "/");

        // The GET path embeds the export in the URL; the POST path (the page `/large`) embeds it in the data object. In both the field is
        // encrypted (not the plain "default"), and decrypts to the export of the module with the associated data of this island.
        var island = Assert.Single(islands);
        Assert.DoesNotContain("\"default\"", island.Script, StringComparison.Ordinal);
        Assert.DoesNotContain("'default'", island.Script, StringComparison.Ordinal);
        Assert.Equal("default", Decrypt(island.Export, "export:Island"));

        var (_, large) = await OpenAsync(site, "/large");
        var post = Assert.Single(large);
        Assert.DoesNotContain("\"default\"", post.Script, StringComparison.Ordinal);
        Assert.Equal("default", Decrypt(post.Export, "export:Island"));
    }

    [Fact(DisplayName = "removes internal server: props before encrypting user props")]
    public async Task GetIslandContent_RemovesTheInternalPropsBeforeEncryptingTheUserProps()
    {
        var site = await sites.GetServerIslandsSiteAsync();

        var (_, islands) = await OpenAsync(site, "/props");

        // The encrypted props have the props of the user and none of server:component-path, server:component-export,
        // server:component-directive and server:defer.
        var island = Assert.Single(islands);
        var props = Decrypt(island.Props, "props:Island");
        using var document = JsonDocument.Parse(props);
        Assert.DoesNotContain("server:", props, StringComparison.Ordinal);
        Assert.Equal(["message", "userProp"], document.RootElement.EnumerateObject().Select(property => property.Name));

        // The island receives the props of the user and nothing else.
        var fragment = ConformanceSite.ParseHtml(await FetchFragmentAsync(site, island));
        Assert.Equal("""{"message":"hello","userProp":"visible"}""", fragment.QuerySelector("#island")?.GetAttribute("data-props"));
    }

    [Fact(DisplayName = "throws when the component path is not in serverIslandNameMap")]
    public async Task GetIslandContent_ThrowsWhenTheComponentPathIsNotInTheNameMap()
    {
        var probe = await RunProbeAsync();

        var result = probe["throws when the component path is not in serverIslandNameMap"];

        Assert.True(result.Bool("threw"));
        Assert.Contains("Could not find server component name", result.Str("message"), StringComparison.Ordinal);
    }

    // #endregion

    // #region ServerIslandComponent: init()

    [Fact(DisplayName = "records CSP script hashes once when called repeatedly")]
    public async Task Init_RecordsTheCspScriptHashesOnce()
    {
        var probe = await RunProbeAsync();

        // Two calls of init() on one component record the hashes of the replacer and of the script of the island: two.
        Assert.Equal(2, probe["records CSP script hashes once when called repeatedly"].GetInt32());

        // With the CSP on, the policy of a page with two islands has the hash of every inline script of the page once: the two scripts
        // of the islands (each one has its own) and the replacer, which both islands recorded.
        var site = await sites.GetServerIslandsCspSiteAsync();
        var page = await site.SendRawAsync("/twice");
        Assert.Equal(200, page.StatusCode);
        var sources = ScriptSources(page);
        var scripts = InlineScripts(page.Text);
        Assert.Equal(3, scripts.Length);
        Assert.Equal(3, scripts.Distinct().Count());
        foreach (var script in scripts)
        {
            Assert.Single(sources, source => source == Sha256Source(script));
        }
    }

    // #endregion

    // #region ServerIslandComponent: render()

    [Fact(DisplayName = "emits the server-island-start HTML comment marker")]
    public async Task Render_EmitsTheServerIslandStartMarker()
    {
        var site = await sites.GetServerIslandsSiteAsync();

        var (html, _) = await OpenAsync(site, "/");

        Assert.Contains("<!--[if astro]>server-island-start<![endif]-->", html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "emits a <script data-island-id> tag")]
    public async Task Render_EmitsAScriptTagWithTheIslandId()
    {
        var site = await sites.GetServerIslandsSiteAsync();

        var (html, islands) = await OpenAsync(site, "/");

        var island = Assert.Single(islands);
        Assert.Contains($"<script type=\"module\" data-astro-rerun data-island-id=\"{island.Id}\">", html, StringComparison.Ordinal);
        Assert.Contains($"replaceServerIsland(\"{island.Id}\", response)", island.Script, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "renders fallback slot content inline")]
    public async Task Render_RendersTheFallbackSlotInline()
    {
        var site = await sites.GetServerIslandsSiteAsync();

        var (html, islands) = await OpenAsync(site, "/fallback");
        var island = Assert.Single(islands);

        // The fallback is in the page between the marker and the script of the island, which is what the browser shows until the
        // island replaces it. (The attribute of this island: the replacer script that precedes the marker also names `data-island-id`.)
        var marker = html.IndexOf("server-island-start", StringComparison.Ordinal);
        var fallback = html.IndexOf("Loading...", StringComparison.Ordinal);
        var script = html.IndexOf($"data-island-id=\"{island.Id}\"", StringComparison.Ordinal);
        Assert.InRange(fallback, marker + 1, script - 1);

        // The fragment of the island is not the fallback.
        Assert.DoesNotContain("Loading...", await FetchFragmentAsync(site, island), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "does not render non-fallback slot content inline")]
    public async Task Render_DoesNotRenderOtherSlotsInline()
    {
        const string slotContent = "Slot content that should be encrypted";
        var site = await sites.GetServerIslandsSiteAsync();

        // The slot named `content` is not in the page: it is encrypted into the URL of the island and rendered by the endpoint.
        var (html, islands) = await OpenAsync(site, "/slot");

        Assert.DoesNotContain(slotContent, html, StringComparison.Ordinal);
        var island = Assert.Single(islands);
        Assert.NotEqual("", island.Slots);
        Assert.Contains(slotContent, Decrypt(island.Slots, "slots:Island"), StringComparison.Ordinal);
        Assert.Contains(slotContent, await FetchFragmentAsync(site, island), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "places the server-island-start marker before the island script tag")]
    public async Task Render_PlacesTheMarkerBeforeTheIslandScript()
    {
        var site = await sites.GetServerIslandsSiteAsync();

        var (html, islands) = await OpenAsync(site, "/");

        // The script of the island, not the replacer script before the marker, which names `data-island-id` in its selector.
        var island = Assert.Single(islands);
        var markerIndex = html.IndexOf("server-island-start", StringComparison.Ordinal);
        var scriptIndex = html.IndexOf($"data-island-id=\"{island.Id}\"", StringComparison.Ordinal);
        Assert.True(markerIndex > -1, "the server-island-start marker should be present");
        Assert.True(scriptIndex > -1, "the data-island-id script should be present");
        Assert.True(markerIndex < scriptIndex, "the marker should appear before the island script");
    }

    // #endregion

    // #region renderComponentToString() with a server island

    [Fact(DisplayName = "does not deadlock when rendered inside a pending slot evaluation")]
    public async Task RenderComponentToString_DoesNotDeadlockInsideAPendingSlotEvaluation()
    {
        var probe = await RunProbeAsync();

        // The probe models an MDX island inside an Astro component slot on a propagation route (#18156): it answers the HTML, or fails
        // when the render does not finish in 2 seconds.
        var html = probe.Str("does not deadlock when rendered inside a pending slot evaluation");

        Assert.Contains("data-island-id", html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "collects head content from propagating components inside the island slots")]
    public async Task RenderComponentToString_CollectsTheHeadContentOfPropagatingComponentsInTheSlots()
    {
        var probe = await RunProbeAsync();

        var extraHead = probe.Strings("collects head content from propagating components inside the island slots");

        Assert.Contains("<style>.nested-content{}</style>", extraHead);
    }

    [Fact(DisplayName = "initializes each island once, producing one set of CSP script hashes per island")]
    public async Task RenderComponentToString_InitializesEachIslandOnce()
    {
        var probe = await RunProbeAsync();

        var result = probe["initializes each island once, producing one set of CSP script hashes per island"];

        Assert.Equal(2, result.GetProperty("afterFirst").GetInt32());
        // A later island render re-runs head collection over every registered propagator, including the first island.
        Assert.Equal(4, result.GetProperty("afterSecond").GetInt32());
    }

    // #endregion
}
