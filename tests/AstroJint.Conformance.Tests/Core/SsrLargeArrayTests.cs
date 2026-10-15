using System.Net;
using System.Text;
using System.Text.Json.Nodes;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/ssr-large-array.test.ts</c> on the <c>large-array</c> fixture (<c>core-large-array</c>: the Solid
/// integration and <c>output: 'server'</c>; the sources are the upstream ones). The page passes an array of 600 objects as a prop of a Solid
/// island, so the <c>props</c> attribute of the island and the response are large. <c>app.render(new Request('http://example.com/'))</c> is
/// <c>GET /</c> and <c>response.arrayBuffer()</c> is reading the body as bytes, which are decoded as UTF-8 as upstream does. The port also
/// reads the <c>props</c> attribute, which upstream does not, to see that all 600 objects are in the response. The Solid island renders on
/// the server because the adapter puts <c>worker</c> in <c>vite.ssr.resolve.conditions</c>, which makes Vite resolve <c>solid-js/web</c> to
/// its server build (GAP-CORE-3, fixed in 579f973).
/// </summary>
[Trait("Upstream", "core/ssr-large-array.test.ts")]
public class SsrLargeArrayTests(SiteHost sites)
{
    private const string Fixture = "core-large-array";

    [Fact(DisplayName = "SSR with Large Array and client rendering: Using response.arrayBuffer() gets the right HTML")]
    public async Task LargeIslandProp_ArrayBufferBody_IsTheRightHtml()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var data = await response.Content.ReadAsByteArrayAsync();
        var html = ConformanceSite.ParseHtml(Encoding.UTF8.GetString(data));

        Assert.Single(html.QuerySelectorAll("head meta[name=\"viewport\"]"));
        Assert.Single(html.QuerySelectorAll("head link[rel=\"icon\"]"));
        Assert.Single(html.QuerySelectorAll("main"));
        var islands = html.QuerySelectorAll("astro-island");
        Assert.Single(islands);
        Assert.Equal("Hello, Solid!", string.Concat(html.QuerySelectorAll("h1").Select(h1 => h1.TextContent)));

        // Not asserted upstream: the prop arrives whole. Astro writes an array as [1, [...items]] and an object as [0, {...}] in `props`.
        var props = JsonNode.Parse(islands[0].GetAttribute("props")!)!.AsObject();
        var largeProp = props["largeProp"]!.AsArray();
        Assert.Equal(1, largeProp[0]!.GetValue<int>());
        var items = largeProp[1]!.AsArray();
        Assert.Equal(600, items.Count);
        Assert.All(items, item => Assert.Equal("bar", item![1]!["foo"]![1]!.GetValue<string>()));
    }
}
