using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/prerender-node-env.test.ts</c>. The upstream tests start the dev server of
/// a fixture whose adapter has <c>prerenderEnvironment: 'node'</c>, an option of the Cloudflare adapter that prerenders in
/// Node while on-demand pages run in workerd. The host has no such option: its build prerenders in Node, by the default
/// prerenderer of Astro, and on-demand pages run in Jint. Three tests ask something that holds for the host as well, and are
/// adapted: the pages are the ones of the shared fixture <c>cf-content</c>, built and served by the host, not asked of a
/// dev server.
/// <list type="bullet">
/// <item>"renders prerendered page using Node.js APIs": <c>/node-apis</c> is prerendered by a page that reads the
/// <c>package.json</c> of the fixture with <c>node:fs</c>; its name is in the page that the host serves (upstream reads the name of
/// the adapter package, because the dev server of its fixture runs in that folder).</item>
/// <item>"serves server islands for prerendered routes in dev": the server island of <c>/node-apis</c> is asked of the host
/// with the URL that the prerendered page has in its script.</item>
/// <item>"serves action requests through workerd without exhausting the POST body": the action is asked of the host as a
/// POST with a JSON body. <c>hasCf</c> is not asserted: <c>request.cf</c> is a Cloudflare property.</item>
/// </list>
/// Not applicable: "renders svelte component that imports .svelte files from node_modules" and "includes styles in prerendered
/// page" (the first is about the module runner of the dev server of the Node prerender environment, which the host does not
/// have; the second is what <c>PrerenderStylesTests</c> asks of the built site), and the two tests that need
/// <c>request.cf</c> from an island and from an on-demand page ("serves server islands through workerd runtime (not Node
/// prerender env)", "renders SSR page through workerd with Astro.request.cf").
/// </summary>
[Trait("Upstream", "cloudflare/prerender-node-env.test.ts")]
public class PrerenderNodeEnvTests(SiteHost sites)
{
    [Fact(DisplayName = "renders prerendered page using Node.js APIs")]
    public async Task RendersAPageThatUsesNodeApis()
    {
        var site = await sites.GetAsync(CfContent.Fixture);

        using var response = await site.GetAsync("/node-apis");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.ReadHtmlAsync();
        Assert.Equal("conformance-cf-content", document.QuerySelector("#pkg-name")?.TextContent);
    }

    [Fact(DisplayName = "serves server islands for prerendered routes in dev")]
    public async Task ServesServerIslandsForPrerenderedRoutes()
    {
        var site = await sites.GetAsync(CfContent.Fixture);

        using var page = await site.GetAsync("/node-apis");
        Assert.Equal(HttpStatusCode.OK, page.StatusCode);
        var html = await page.ReadTextAsync();
        Assert.Contains("id=\"deferred-fallback\"", html);

        var island = Regex.Match(html, "fetch\\([\"'](/_server-islands/[^\"']+)[\"']");
        Assert.True(island.Success, "Expected prerendered HTML to include a server island fetch URL");

        using var islandResponse = await site.GetAsync(island.Groups[1].Value);
        Assert.Equal(HttpStatusCode.OK, islandResponse.StatusCode);
        Assert.Contains("id=\"deferred-content\"", await islandResponse.ReadTextAsync());
    }

    [Fact(DisplayName = "serves action requests through workerd without exhausting the POST body")]
    public async Task ServesActionRequests()
    {
        var site = await sites.GetAsync(CfContent.Fixture);

        using var response = await site.SendAsync(
            HttpMethod.Post,
            "/_actions/hello",
            new StringContent("{}", Encoding.UTF8, "application/json"));

        var body = await response.ReadTextAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, CfContent.Describe(site, response, body));
        Assert.Equal("Hello, World!", MessageOf(body));
    }

    // The body of an action is the devalue form of its result, which for { hasCf, message } is [{"hasCf":1,"message":2},false,"the message"].
    private static string? MessageOf(string body)
    {
        using var document = JsonDocument.Parse(body);
        var root = document.RootElement;
        return root[root[0].GetProperty("message").GetInt32()].GetString();
    }
}
