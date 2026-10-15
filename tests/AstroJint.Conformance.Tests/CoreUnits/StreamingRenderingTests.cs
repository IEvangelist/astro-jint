using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/render/streaming-rendering.test.ts</c>: the compiled pages of a render (text and
/// primitives, arrays, components, slots, the <c>set:html</c>, <c>set:text</c>, <c>class:list</c> and <c>style</c> directives,
/// inline styles and scripts, async components, falsy values) come out as the HTML that the upstream page components produce.
/// All of them are adapted.
/// <para>
/// Differences from upstream: upstream turned on <c>experimentalQueuedRendering</c> for these tests; Astro 7.3.8, the version
/// of the fixture, has no such option (its render engine is the only one), so the pages run on it. The upstream components are
/// the real <c>.astro</c> files of the <c>app-render</c> fixture under <c>src/pages/streaming-rendering</c>; the inline
/// <c>&lt;style&gt;</c> and <c>&lt;script&gt;</c> of the head-content page are <c>is:inline</c>, because the compiler would hoist
/// the plain ones out of the HTML. 'renders async components in parallel, preserving order' does not measure the time of the
/// request (upstream's bound was 90 ms for five 25 ms components, which is not stable over a real round trip through the
/// host); each <c>Slow</c> component writes the time it started and the time it ended into its element, and the test checks the
/// source order and that every component started before the first one ended, which is what a serial render cannot give. The
/// host buffers the body of the response, so what the tests see is the whole page, as for upstream's <c>response.text()</c>.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/render/streaming-rendering.test.ts")]
public class StreamingRenderingTests(SiteHost sites)
{
    private async Task<RawResponse> GetPageAsync(string path)
    {
        var site = await sites.GetAsync(Fixture);
        var response = await site.SendRawAsync(path);
        Assert.Equal(200, response.StatusCode);
        return response;
    }

    // #region Basic rendering

    [Fact(DisplayName = "should render index page successfully")]
    public async Task IndexPage_IsRendered()
    {
        var html = (await GetPageAsync("/streaming-rendering/")).Text;

        Assert.Contains("<title>Queue Rendering Test</title>", html);
        Assert.Contains("<h1>Queue Rendering Test</h1>", html);
    }

    [Fact(DisplayName = "should render simple text and primitives correctly")]
    public async Task TextAndPrimitives_AreRendered()
    {
        var html = (await GetPageAsync("/streaming-rendering/")).Text;

        Assert.Contains("<p>Simple text rendering</p>", html);
        Assert.Contains("<p>Number: 42</p>", html);
        Assert.Contains("<p>Boolean: true</p>", html);
    }

    [Fact(DisplayName = "should render arrays correctly")]
    public async Task Arrays_AreRenderedInOrder()
    {
        var html = (await GetPageAsync("/streaming-rendering/")).Text;

        Assert.Contains("<li>First</li>", html);
        Assert.Contains("<li>Second</li>", html);
        Assert.Contains("<li>Third</li>", html);
        var first = html.IndexOf("<li>First</li>", StringComparison.Ordinal);
        var second = html.IndexOf("<li>Second</li>", StringComparison.Ordinal);
        var third = html.IndexOf("<li>Third</li>", StringComparison.Ordinal);
        Assert.True(first < second);
        Assert.True(second < third);
    }

    [Fact(DisplayName = "should render multiple component instances correctly")]
    public async Task MultipleComponentInstances_AreRendered()
    {
        var html = (await GetPageAsync("/streaming-rendering/")).Text;

        foreach (var level in new[] { "0", "1", "2" })
        {
            Assert.Contains($"data-level=\"{level}\"", html);
            Assert.Contains($"Level {level}", html);
        }
    }

    [Fact(DisplayName = "should render components with slots correctly")]
    public async Task ComponentsWithSlots_AreRendered()
    {
        var html = (await GetPageAsync("/streaming-rendering/")).Text;

        Assert.Contains("class=\"with-slot\"", html);
        Assert.Contains("<h2>Test Title</h2>", html);
        Assert.Contains("class=\"slot-content\"", html);
        Assert.Contains("<p>Slot content here</p>", html);
        Assert.Contains("<p>Multiple paragraphs</p>", html);
    }

    // #endregion

    // #region Astro directives

    [Fact(DisplayName = "should handle set:html directive")]
    public async Task SetHtml_IsNotEscaped()
    {
        var html = (await GetPageAsync("/streaming-rendering/directives")).Text;

        Assert.Contains("<strong>Bold text from set:html</strong>", html);
    }

    [Fact(DisplayName = "should handle set:text directive")]
    public async Task SetText_IsEscaped()
    {
        var html = (await GetPageAsync("/streaming-rendering/directives")).Text;

        Assert.Contains("&lt;em&gt;This should be escaped&lt;/em&gt;", html);
    }

    [Fact(DisplayName = "should handle class:list directive")]
    public async Task ClassList_IsMergedIntoClass()
    {
        var html = (await GetPageAsync("/streaming-rendering/directives")).Text;

        Assert.Contains("class=\"foo bar baz\"", html);
    }

    [Fact(DisplayName = "should handle inline style objects")]
    public async Task StyleObject_IsSerialized()
    {
        var html = (await GetPageAsync("/streaming-rendering/directives")).Text;

        Assert.True(html.Contains("color:red") || html.Contains("color: red"));
        Assert.True(html.Contains("font-size:20px") || html.Contains("font-size: 20px"));
    }

    // #endregion

    // #region Head content

    [Fact(DisplayName = "should include inline styles")]
    public async Task InlineStyles_AreIncluded()
    {
        var html = (await GetPageAsync("/streaming-rendering/head-content")).Text;

        Assert.Contains(".inline-test", html);
        Assert.True(html.Contains("color:green") || html.Contains("color: green"));
    }

    [Fact(DisplayName = "should include component styles")]
    public async Task ComponentStyles_AreIncluded()
    {
        var html = (await GetPageAsync("/streaming-rendering/head-content")).Text;

        Assert.Contains(".with-head", html);
    }

    [Fact(DisplayName = "should include component scripts")]
    public async Task ComponentScripts_AreIncluded()
    {
        var html = (await GetPageAsync("/streaming-rendering/head-content")).Text;

        Assert.Contains("WithHead script loaded", html);
    }

    [Fact(DisplayName = "should include inline scripts")]
    public async Task InlineScripts_AreIncluded()
    {
        var html = (await GetPageAsync("/streaming-rendering/head-content")).Text;

        Assert.Contains("Inline script executed", html);
    }

    // #endregion

    // #region Streaming behavior

    [Fact(DisplayName = "renders async components in parallel, preserving order")]
    public async Task AsyncComponents_RenderInParallel_InSourceOrder()
    {
        var response = await GetPageAsync("/streaming-rendering/async");
        var html = response.Text;

        for (var i = 0; i < 5; i++)
        {
            Assert.Contains($"data-id=\"{i}\">slow {i}</div>", html);
        }

        var positions = Enumerable.Range(0, 5).Select(i => html.IndexOf($"data-id=\"{i}\"", StringComparison.Ordinal)).ToArray();
        Assert.Equal(positions.Order(), positions);

        var components = Html(response).QuerySelectorAll(".slow");
        Assert.Equal(5, components.Length);
        var starts = components.Select(component => long.Parse(component.GetAttribute("data-start")!)).ToArray();
        var ends = components.Select(component => long.Parse(component.GetAttribute("data-end")!)).ToArray();
        Assert.True(starts.Max() < ends.Min(), $"starts {string.Join(",", starts)} ends {string.Join(",", ends)}");
    }

    [Fact(DisplayName = "skips null/undefined/false/empty but renders 0 and true")]
    public async Task FalsyValues_AreSkippedExceptZeroAndTrue()
    {
        var html = (await GetPageAsync("/streaming-rendering/falsy")).Text;

        Assert.Contains("<p id=\"vals\">0true</p>", html);
    }

    // #endregion
}
