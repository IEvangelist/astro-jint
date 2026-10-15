using AngleSharp.Dom;
using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/render/directives.test.ts</c>: <c>define:vars</c> on scripts and styles and
/// <c>set:html</c> with every kind of value (a string, <c>0</c>, <c>1</c>, <c>false</c>, <c>true</c>, <c>undefined</c>,
/// <c>null</c>, a <c>Response</c>) and as the children of a component.
/// <para>
/// The six tests are ported over HTTP. Upstream writes the pages as <c>createComponent()</c> calls "equivalent to the compiled
/// output of the <c>.astro</c> file"; here they are the <c>.astro</c> files themselves (<c>pages/directives/*.astro</c> of the
/// <c>app-render</c> fixture), compiled by the Astro compiler of the build, and the assertions are upstream's. The page of
/// 'set:html Fragment as slot (children)' puts the <c>&lt;Fragment set:html&gt;</c> in the slot of a component that renders its
/// slot (<c>WithSlot</c>).
/// </para>
/// </summary>
[Trait("Upstream", "core-units/render/directives.test.ts")]
public class DirectivesTests(SiteHost sites)
{
    private async Task<RawResponse> GetAsync(string path)
    {
        var site = await sites.GetAsync(Fixture);
        return await site.SendRawAsync(path);
    }

    private static void AssertText(IDocument html, string id, string expected)
    {
        var element = Assert.Single(html.QuerySelectorAll("#" + id));
        Assert.Equal(expected, element.TextContent);
    }

    [Fact(DisplayName = "passes define:vars to script elements")]
    public async Task DefineVars_ScriptElements_ReceiveTheVariables()
    {
        var response = await GetAsync("/directives/define-vars");

        var scripts = Html(response).QuerySelectorAll("script");
        Assert.Equal(5, scripts.Length);

        string[] expected =
        [
            "const foo = \"bar\"",
            "const foo = \"bar\"",
            "const dashCase = \"bar\"",
            "const bar = \"\\u003cscript>bar\\u003c/script>\"",
            "const undef = undefined",
        ];
        for (var i = 0; i < scripts.Length; i++)
        {
            Assert.StartsWith("(function(){", scripts[i].TextContent, StringComparison.Ordinal);
            Assert.EndsWith("})();", scripts[i].TextContent, StringComparison.Ordinal);
            Assert.Contains(expected[i], scripts[i].OuterHtml, StringComparison.Ordinal);
        }
    }

    [Fact(DisplayName = "passes define:vars to style elements (via inline style attribute)")]
    public async Task DefineVars_StyleElements_ReceiveTheVariablesInTheStyleAttribute()
    {
        var response = await GetAsync("/directives/define-vars");

        var html = Html(response);
        var htmlStyle = html.QuerySelector("html")?.GetAttribute("style");
        Assert.Contains("--bg: white;", htmlStyle, StringComparison.Ordinal);
        Assert.Contains("--fg: black;", htmlStyle, StringComparison.Ordinal);

        // Title component injects --textColor
        Assert.Contains("--textColor: red;", html.QuerySelector("h1")?.GetAttribute("style"), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "properly handles define:vars on style elements with style object")]
    public async Task DefineVars_StyleElementWithAStyleObject_IsMergedIntoTheStyleAttribute()
    {
        var response = await GetAsync("/directives/define-vars");

        Assert.Contains(
            "color:var(--fg);--bg: white;--fg: black;",
            Html(response).QuerySelector("#compound-style")?.GetAttribute("style"),
            StringComparison.Ordinal);
    }

    [Fact(DisplayName = "set:html")]
    public async Task SetHtml_WithPrimitives_RendersThem()
    {
        var response = await GetAsync("/directives/set-html");

        var html = Html(response);
        AssertText(html, "text", "a");
        AssertText(html, "zero", "0");
        AssertText(html, "number", "1");
        AssertText(html, "undefined", "");
        AssertText(html, "null", "");
        AssertText(html, "false", "");
        AssertText(html, "true", "true");
    }

    [Fact(DisplayName = "set:html Fragment as slot (children)")]
    public async Task SetHtml_FragmentAsSlotChildren_IsRendered()
    {
        var response = await GetAsync("/directives/set-html-children");

        Assert.Contains("Test", response.Text, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "set:html can take a Response object (fetch result)")]
    public async Task SetHtml_AResponse_RendersItsBody()
    {
        var response = await GetAsync("/directives/set-html-fetch");

        Assert.Equal(200, response.StatusCode);
        AssertText(Html(response), "fetched-html", "works");
    }
}
