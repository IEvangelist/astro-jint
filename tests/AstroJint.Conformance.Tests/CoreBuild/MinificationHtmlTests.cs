using System.Net;
using System.Text.RegularExpressions;

namespace AstroJint.Conformance.CoreBuild;

/// <summary>
/// Port of <c>packages/astro/test/minification-html.test.ts</c>: the whitespace that <c>compressHTML</c> (<c>true</c>,
/// <c>'jsx'</c> and unset, which compresses like <c>'jsx'</c>) leaves in the HTML of a page. Upstream builds three fixtures
/// (<c>minification-html</c>, <c>minification-html-jsx</c> and <c>minification-html-default</c>), always with
/// <c>inlineStylesheets: 'never'</c>, and reads <c>index.html</c> from <c>dist</c>, apart from "Build SSR" (it renders the page
/// with a test adapter) and "in DEV environment" (it starts the dev server; not ported, see the matrix). The pages of the three
/// fixtures are <c>/</c>, <c>/jsx</c> and <c>/default</c> of <c>cb-minify</c>, and <c>compressHTML</c> and <c>output</c> are
/// config variants.
/// <para>
/// Differences from upstream: the HTML comes from HTTP requests to the host, and every test that upstream runs only on the
/// prerendered page also runs on the on-demand page of the same config (<c>output: 'server'</c>, rendered by Jint), which is
/// stricter. "Build SSR" checks the whole body for line breaks, where upstream skips its first 20 characters (the doctype
/// line); the doctype of the on-demand page is not followed by a line break. "Removes every newline outside of &lt;pre&gt;"
/// also checks that the page has a <c>&lt;pre&gt;</c>, so that removing it cannot be vacuous. The components of the
/// <c>minification-html</c> fixture moved from <c>src/pages</c> to <c>src/components</c>, so that they are not routes.
/// </para>
/// </summary>
[Trait("Upstream", "core/minification-html.test.ts")]
public partial class MinificationHtmlTests(SiteHost sites)
{
    private const string Fixture = "cb-minify";

    [GeneratedRegex("[\r\n]")]
    private static partial Regex LineBreak { get; }

    [GeneratedRegex("<pre[\\s\\S]*?</pre>")]
    private static partial Regex PreElement { get; }

    [GeneratedRegex("keep\\n\\s+this\\n\\s+whitespace")]
    private static partial Regex PreservedText { get; }

    [GeneratedRegex("<pre id=\"preserved\">\\n\\s+keep\\n\\s+this\\n\\s+whitespace")]
    private static partial Regex PreservedPre { get; }

    /// <param name="mode"><c>static</c> (prerendered pages) or <c>server</c> (on-demand pages).</param>
    /// <param name="compressHtml"><c>true</c>, <c>"jsx"</c>, or <see langword="null"/> to leave <c>compressHTML</c> unset.</param>
    private Task<ConformanceSite> SiteAsync(string mode, object? compressHtml)
    {
        var config = new Dictionary<string, object>();
        if (compressHtml is not null)
        {
            config["compressHTML"] = compressHtml;
        }

        if (mode == "server")
        {
            config["output"] = "server";
        }

        return sites.GetAsync(Fixture, config.Count == 0 ? null : config);
    }

    private static async Task<string> HtmlAsync(ConformanceSite site, string path)
    {
        using var response = await site.GetAsync(path);
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"{path} answered {(int)response.StatusCode}.");

        return await response.ReadTextAsync();
    }

    [Fact(DisplayName = "HTML minification: Build SSG: should emit compressed HTML in the emitted file")]
    public async Task BuildSsg_EmitsCompressedHtml()
    {
        var site = await SiteAsync("static", true);

        var html = await HtmlAsync(site, "/");

        Assert.DoesNotMatch(LineBreak, html);
    }

    [Fact(DisplayName = "HTML minification: Build SSR: should emit compressed HTML in the emitted file")]
    public async Task BuildSsr_EmitsCompressedHtml()
    {
        var site = await SiteAsync("server", true);

        var html = await HtmlAsync(site, "/");

        Assert.DoesNotMatch(LineBreak, html);
    }

    [Theory(DisplayName = "HTML minification (JSX mode): should strip indentation from multi-line text")]
    [InlineData("static")]
    [InlineData("server")]
    public async Task JsxMode_StripsTheIndentationFromMultiLineText(string mode)
    {
        var site = await SiteAsync(mode, "jsx");

        var html = await HtmlAsync(site, "/jsx");

        // JSX whitespace stripping joins the lines of a multi-line text with a space and drops their indentation.
        Assert.Contains("Hello world", html);
        Assert.DoesNotContain("\n        Hello", html);
    }

    [Theory(DisplayName = "HTML minification (JSX mode): should preserve whitespace inside <pre> tags")]
    [InlineData("static")]
    [InlineData("server")]
    public async Task JsxMode_PreservesTheWhitespaceInsidePre(string mode)
    {
        var site = await SiteAsync(mode, "jsx");

        var html = await HtmlAsync(site, "/jsx");

        Assert.Contains("<pre id=\"preserved\">", html);
        Assert.Matches(PreservedText, html);
    }

    [Theory(DisplayName = "HTML minification (JSX mode): should preserve inline text and elements on the same line")]
    [InlineData("static")]
    [InlineData("server")]
    public async Task JsxMode_PreservesInlineElementsOnTheSameLine(string mode)
    {
        var site = await SiteAsync(mode, "jsx");

        var html = await HtmlAsync(site, "/jsx");

        Assert.Contains("<span>hello</span> <em>world</em>", html);
    }

    [Theory(DisplayName = "HTML minification (default): strips whitespace between inline elements, following JSX rules")]
    [InlineData("static")]
    [InlineData("server")]
    public async Task Default_StripsTheWhitespaceBetweenInlineElements(string mode)
    {
        var site = await SiteAsync(mode, null);

        // The discriminator: HTML-aware compression (`true`) keeps a single space here, the default JSX rules remove it.
        var html = await HtmlAsync(site, "/default");

        Assert.Contains("<span>hello</span><em>world</em>", html);
    }

    [Theory(DisplayName = "HTML minification (default): collapses multi-line text into a single line")]
    [InlineData("static")]
    [InlineData("server")]
    public async Task Default_CollapsesMultiLineText(string mode)
    {
        var site = await SiteAsync(mode, null);

        var html = await HtmlAsync(site, "/default");

        Assert.Contains("<p id=\"multi-line\">Hello world</p>", html);
    }

    [Theory(DisplayName = "HTML minification (default): preserves whitespace inside <pre> tags")]
    [InlineData("static")]
    [InlineData("server")]
    public async Task Default_PreservesTheWhitespaceInsidePre(string mode)
    {
        var site = await SiteAsync(mode, null);

        var html = await HtmlAsync(site, "/default");

        Assert.Matches(PreservedPre, html);
    }

    [Theory(DisplayName = "HTML minification (default): removes every newline outside of <pre>")]
    [InlineData("static")]
    [InlineData("server")]
    public async Task Default_RemovesEveryNewlineOutsideOfPre(string mode)
    {
        var site = await SiteAsync(mode, null);

        var html = await HtmlAsync(site, "/default");

        Assert.Matches(PreElement, html);
        Assert.DoesNotMatch(LineBreak, PreElement.Replace(html, ""));
    }

    [Theory(DisplayName = "HTML minification (compressHTML: true differs from the default): keeps a single space between inline elements")]
    [InlineData("static")]
    [InlineData("server")]
    public async Task CompressHtmlTrue_KeepsASingleSpaceBetweenInlineElements(string mode)
    {
        var site = await SiteAsync(mode, true);

        // HTML-aware compression keeps the white space between inline elements as a single space.
        var html = await HtmlAsync(site, "/default");

        Assert.Contains("<span>hello</span> <em>world</em>", html);
    }
}
