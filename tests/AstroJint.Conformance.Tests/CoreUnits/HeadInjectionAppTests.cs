using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/render/head-injection-app.test.ts</c>: the head that a component propagates
/// (<c>createHeadAndContent()</c> from a component that says <c>propagation: 'self'</c>) is injected into the
/// <c>&lt;head&gt;</c> of the page by the pipeline, in a page that has a <c>&lt;head&gt;</c>, through nested layout components, while
/// a slot is rendered to a string before the head is flushed, and from a content entry inside a named <c>Fragment</c> slot
/// (#16181).
/// <para>
/// All four tests are adapted, with the assertions of upstream. Upstream writes the components inline with
/// <c>createComponent()</c> and renders the page through a stub pipeline (<c>renderThroughMiddleware</c>). Here the same
/// components are the TypeScript module <c>lib/head-injection.ts</c> of the <c>app-render</c> fixture (a component that returns
/// <c>createHeadAndContent()</c> with <c>propagation: 'self'</c>, the <c>in-tree</c> components above it, a component that
/// calls <c>renderSlotToString()</c>, and the component of a content entry that returns a <c>&lt;style&gt;</c>) and the pages
/// <c>pages/head-injection/*.astro</c> render them, so the head is propagated and injected by the real pipeline inside the host.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/render/head-injection-app.test.ts")]
public class HeadInjectionAppTests(SiteHost sites)
{
    private async Task<AngleSharp.Dom.IDocument> RenderAsync(string page)
    {
        var site = await sites.GetAsync(Fixture);
        var response = await site.SendRawAsync("/head-injection/" + page);
        Assert.Equal(200, response.StatusCode);
        return Html(response);
    }

    [Fact(DisplayName = "injects propagated head from component created in page scope")]
    public async Task PropagatedHead_FromAComponentOfThePageScope_IsInjected()
    {
        var html = await RenderAsync("page-scope");

        Assert.Single(html.QuerySelectorAll("head link[rel=\"stylesheet\"][href=\"/some/fake/styles.css\"]"));
        Assert.Empty(html.QuerySelectorAll("body link[rel=\"stylesheet\"]"));
        Assert.Single(html.QuerySelectorAll("#other"));
    }

    [Fact(DisplayName = "injects propagated head through nested layout components")]
    public async Task PropagatedHead_ThroughNestedLayoutComponents_IsInjected()
    {
        var html = await RenderAsync("nested-layout");

        Assert.Single(html.QuerySelectorAll("head link[rel=\"stylesheet\"][href=\"/some/fake/styles.css\"]"));
        Assert.Empty(html.QuerySelectorAll("body link[rel=\"stylesheet\"]"));
        Assert.Single(html.QuerySelectorAll("#other"));
    }

    [Fact(DisplayName = "supports slot rendering during head buffering without style bleed")]
    public async Task PropagatedHead_WithSlotRenderingDuringBuffering_DoesNotBleed()
    {
        var html = await RenderAsync("slot-render");

        Assert.Single(html.QuerySelectorAll("head link[href=\"/styles/slot-render.css\"]"));
        Assert.Single(html.QuerySelectorAll("head link[href=\"/styles/from-slot.css\"]"));
        Assert.Empty(html.QuerySelectorAll("body link[rel=\"stylesheet\"]"));
        Assert.Equal("Paragraph.", string.Concat(html.QuerySelectorAll("p").Select(paragraph => paragraph.TextContent)));
    }

    [Fact(DisplayName = "propagates head from Content inside Fragment named slot (#16181)")]
    public async Task PropagatedHead_FromContentInsideAFragmentNamedSlot_IsInjected()
    {
        var html = await RenderAsync("fragment-slot");

        var style = Assert.Single(html.QuerySelectorAll("head style"));
        Assert.Equal(".box{color:hotpink}", style.TextContent);
        Assert.Single(html.QuerySelectorAll(".box"));
    }
}
