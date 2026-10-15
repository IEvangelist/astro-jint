using AngleSharp.Dom;
using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/render/slots.test.ts</c>: named, dynamic and conditional slots, fallback content, slots
/// filled with several elements or with components, the <c>Astro.slots</c> API (<c>has()</c>-style conditional ids,
/// <c>Astro.slots.render()</c> with and without arguments, a slot rendered many times) and a named slot left unused.
/// <para>
/// All 19 tests are ported over HTTP. Upstream builds each page with <c>createComponent()</c> and <c>render``</c> templates
/// "equivalent to the compiled output of the <c>.astro</c> file"; here every page is the <c>.astro</c> source itself
/// (<c>pages/slots/*.astro</c> and <c>components/slots/*.astro</c> of the <c>app-render</c> fixture), compiled by the Astro
/// compiler of the build, with the markup and ids that the unit tests render, and the assertions are upstream's. Each test
/// requests its own page.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/render/slots.test.ts")]
public class SlotsTests(SiteHost sites)
{
    private async Task<IDocument> RenderAsync(string page)
    {
        var site = await sites.GetAsync(Fixture);
        var response = await site.SendRawAsync("/slots/" + page);
        Assert.Equal(200, response.StatusCode);
        return Html(response);
    }

    // cheerio's text() joins the text of every element that matches.
    private static string TextOf(IDocument html, string selector) =>
        string.Concat(html.QuerySelectorAll(selector).Select(element => element.TextContent));

    private static int CountOf(IDocument html, string selector) => html.QuerySelectorAll(selector).Length;

    private static void AssertNamedSlots(IDocument html)
    {
        Assert.Equal("A", TextOf(html, "#a").Trim());
        Assert.Equal("B", TextOf(html, "#b").Trim());
        Assert.Equal("C", TextOf(html, "#c").Trim());
        Assert.Equal("Default", TextOf(html, "#default").Trim());
    }

    [Fact(DisplayName = "Basic named slots work")]
    public async Task NamedSlots_Basic_AreRendered()
    {
        var html = await RenderAsync("basic");

        AssertNamedSlots(html);
    }

    [Fact(DisplayName = "Dynamic named slots work")]
    public async Task NamedSlots_Dynamic_AreRendered()
    {
        var html = await RenderAsync("dynamic");

        AssertNamedSlots(html);
    }

    [Fact(DisplayName = "Conditional named slots work")]
    public async Task NamedSlots_Conditional_AreRendered()
    {
        var html = await RenderAsync("conditional");

        AssertNamedSlots(html);
    }

    [Fact(DisplayName = "Slots of a component render fallback content by default")]
    public async Task Fallback_OfAComponent_IsRenderedByDefault()
    {
        var html = await RenderAsync("fallback");

        Assert.Equal(1, CountOf(html, "#default"));
    }

    [Fact(DisplayName = "Slots of a page render fallback content")]
    public async Task Fallback_OfAPage_IsRendered()
    {
        var html = await RenderAsync("fallback-page");

        Assert.Equal(1, CountOf(html, "#default"));
    }

    [Fact(DisplayName = "Slots override fallback content")]
    public async Task Fallback_IsOverriddenByTheSlottedContent()
    {
        var html = await RenderAsync("fallback-override");

        Assert.Equal(1, CountOf(html, "#override"));
        Assert.Equal("Slotty slot.", TextOf(html, "#fallback-2"));
    }

    [Fact(DisplayName = "Slots work with multiple elements")]
    public async Task Slots_WithMultipleElements_AreRendered()
    {
        var html = await RenderAsync("multiple-elements");

        Assert.Equal("ABC", TextOf(html, "#a").Trim());
    }

    [Fact(DisplayName = "Slots work on Components")]
    public async Task Slots_OnComponents_AreRendered()
    {
        var html = await RenderAsync("components");

        Assert.Equal(1, CountOf(html, "#a"));
        Assert.Equal(1, CountOf(html, "#a > astro-component"));
        Assert.Equal(1, CountOf(html, "#default > astro-component"));
    }

    // #region Slots API work on Components

    [Fact(DisplayName = "IDs will exist whether the slots are filled or not")]
    public async Task SlotsApi_IdsAlwaysRendered_ExistWhetherTheSlotsAreFilledOrNot()
    {
        var html = await RenderAsync("api-always");

        Assert.Equal(1, CountOf(html, "#a"));
        Assert.Equal(1, CountOf(html, "#b"));
        Assert.Equal(1, CountOf(html, "#c"));
        Assert.Equal(1, CountOf(html, "#default"));
    }

    [Fact(DisplayName = "IDs will not exist because the slots are not filled")]
    public async Task SlotsApi_UnfilledSlots_HaveNoIds()
    {
        var html = await RenderAsync("api-none");

        Assert.Equal(0, CountOf(html, "#a"));
        Assert.Equal(0, CountOf(html, "#b"));
        Assert.Equal(0, CountOf(html, "#c"));
        Assert.Equal(0, CountOf(html, "#default"));
    }

    [Fact(DisplayName = "IDs will exist because the slots are filled")]
    public async Task SlotsApi_FilledSlots_HaveIds()
    {
        var html = await RenderAsync("api-filled");

        Assert.Equal(1, CountOf(html, "#a"));
        Assert.Equal(1, CountOf(html, "#b"));
        Assert.Equal(1, CountOf(html, "#c"));
        Assert.Equal(0, CountOf(html, "#default")); // default slot is not filled
    }

    [Fact(DisplayName = "Default ID will exist because the default slot is filled")]
    public async Task SlotsApi_FilledDefaultSlot_HasItsId()
    {
        var html = await RenderAsync("api-default");

        Assert.Equal(0, CountOf(html, "#a"));
        Assert.Equal(0, CountOf(html, "#b"));
        Assert.Equal(0, CountOf(html, "#c"));
        Assert.Equal(1, CountOf(html, "#default"));
    }

    // #endregion

    // #region Slots.render() API

    [Fact(DisplayName = "Simple imperative slot render")]
    public async Task SlotsRender_Simple_RendersTheSlot()
    {
        var html = await RenderAsync("render-simple");

        Assert.Equal(1, CountOf(html, "#render"));
        Assert.Equal("render", TextOf(html, "#render"));
    }

    [Fact(DisplayName = "Child function render without args")]
    public async Task SlotsRender_ChildFunctionWithoutArgs_RendersTheSlot()
    {
        var html = await RenderAsync("render-fn");

        Assert.Equal(1, CountOf(html, "#render-fn"));
        Assert.Equal("render-fn", TextOf(html, "#render-fn"));
    }

    [Fact(DisplayName = "Child function render with args")]
    public async Task SlotsRender_ChildFunctionWithArgs_RendersTheSlot()
    {
        var html = await RenderAsync("render-args");

        Assert.Equal(1, CountOf(html, "#render-args"));
        Assert.Equal(1, CountOf(html, "#render-args span"));
        Assert.Equal("render-args", TextOf(html, "#render-args"));
    }

    [Fact(DisplayName = "Slots rendered multiple times produce independent results")]
    public async Task SlotsRender_MultipleTimes_ProducesIndependentResults()
    {
        var html = await RenderAsync("render-multiple");

        var elements = html.QuerySelectorAll("div");
        Assert.Equal(10, elements.Length);

        var first = elements[0].TextContent;
        var second = elements[1].TextContent;
        var third = elements[2].TextContent;

        Assert.NotEqual(first, second);
        Assert.NotEqual(second, third);
        Assert.NotEqual(third, first);
    }

    // #endregion

    [Fact(DisplayName = "Arguments can be passed to named slots with Astro.slots.render()")]
    public async Task SlotsRender_NamedSlots_ReceiveArguments()
    {
        var html = await RenderAsync("named-functions");

        Assert.Equal("Test Content BEFORE", TextOf(html, "div#before > div"));
        Assert.Equal("Test Content AFTER", TextOf(html, "div#after > div"));
    }

    [Fact(DisplayName = "Arguments can be passed to conditional named slots with Astro.slots.render()")]
    public async Task SlotsRender_ConditionalNamedSlots_ReceiveArguments()
    {
        var html = await RenderAsync("conditional-callback");

        Assert.Equal("Block: block", TextOf(html, "#conditional-block"));
    }

    [Fact(DisplayName = "Unused slot renders without error")]
    public async Task UnusedSlot_RendersWithoutError()
    {
        var html = await RenderAsync("unused");

        // Card has a named "icon" slot that is not filled. The check of upstream would also pass if the page had no paragraph.
        Assert.NotEmpty(html.QuerySelectorAll("body p"));
        Assert.Equal(0, html.QuerySelectorAll("body p").Sum(paragraph => paragraph.Children.Length));
    }
}
