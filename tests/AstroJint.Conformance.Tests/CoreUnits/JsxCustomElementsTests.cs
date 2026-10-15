using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/render/jsx-custom-elements.test.ts</c>: <c>renderJSX()</c> renders standard HTML elements
/// directly, sends custom elements (names with a hyphen) through the renderers (<c>check()</c> then
/// <c>renderToStaticMarkup()</c>), falls back to raw HTML when no renderer claims one, keeps the <c>slot</c> attribute of the
/// children of a custom element, and does not call a renderer for a standard element.
/// <para>
/// The five tests are adapted: <c>renderJSX()</c> is internal runtime code, so the probe endpoint of the <c>app-render</c>
/// fixture (module <c>jsx-custom-elements</c>) makes upstream's calls inside the host (the same vnodes, the same mock renderers)
/// and the tests assert what upstream asserts. The only difference is the client directives of the mock result: upstream takes
/// them from <c>getDefaultClientDirectives()</c> (an internal module that <c>astro</c> does not export); the probe uses a map with
/// the same five names (<c>load</c>, <c>idle</c>, <c>visible</c>, <c>media</c>, <c>only</c>), and none of the vnodes uses a
/// <c>client:</c> directive.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/render/jsx-custom-elements.test.ts")]
public class JsxCustomElementsTests(SiteHost sites)
{
    private async Task<Probe> ProbeAsync()
    {
        var site = await sites.GetAsync(Fixture);
        return await site.ProbeAsync("jsx-custom-elements");
    }

    [Fact(DisplayName = "routes standard HTML elements through renderElement")]
    public async Task StandardElements_AreRenderedDirectly()
    {
        var probe = await ProbeAsync();

        var output = probe.Str("routes standard HTML elements through renderElement");
        Assert.Contains("<div", output, StringComparison.Ordinal); // opening tag
        Assert.Contains("class=\"test\"", output, StringComparison.Ordinal); // attributes
        Assert.Contains("Hello", output, StringComparison.Ordinal); // children
        Assert.Contains("</div>", output, StringComparison.Ordinal); // closing tag
    }

    [Fact(DisplayName = "routes custom elements (with hyphens) through renderComponentToString for renderer pipeline")]
    public async Task CustomElements_GoThroughTheRendererPipeline()
    {
        var probe = await ProbeAsync();

        var value = probe["routes custom elements (with hyphens) through renderComponentToString for renderer pipeline"];
        Assert.True(value.Bool("rendererCheckCalled"), "renderer check() should have been called for custom element");
        Assert.True(value.Bool("rendererRenderCalled"), "renderer renderToStaticMarkup() should have been called");
        var output = value.Str("output");
        Assert.Contains("data-ssr=\"true\"", output, StringComparison.Ordinal); // SSR marker from renderer
        Assert.Contains("my-element", output, StringComparison.Ordinal); // the custom element tag
    }

    [Fact(DisplayName = "falls back to raw HTML for custom elements when no renderer claims them")]
    public async Task CustomElements_NoRendererClaimsThem_FallBackToRawHtml()
    {
        var probe = await ProbeAsync();

        var output = probe.Str("falls back to raw HTML for custom elements when no renderer claims them");
        Assert.Contains("<unknown-element", output, StringComparison.Ordinal);
        Assert.Contains("foo=\"bar\"", output, StringComparison.Ordinal);
        Assert.Contains("content", output, StringComparison.Ordinal);
        Assert.Contains("</unknown-element>", output, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "preserves slot attribute on children of custom elements")]
    public async Task CustomElements_KeepTheSlotAttributeOfTheirChildren()
    {
        var probe = await ProbeAsync();

        var output = probe.Str("preserves slot attribute on children of custom elements");
        Assert.Contains("slot=\"icon\"", output, StringComparison.Ordinal); // slot attribute on the child element
        Assert.Contains("<svg", output, StringComparison.Ordinal); // the child svg element
        Assert.Contains("<my-element", output, StringComparison.Ordinal); // the custom element
        Assert.Contains("text content", output, StringComparison.Ordinal); // text children
    }

    [Fact(DisplayName = "does not route standard elements through the renderer pipeline")]
    public async Task StandardElements_DoNotCallTheRenderers()
    {
        var probe = await ProbeAsync();

        Assert.False(
            probe.Bool("does not route standard elements through the renderer pipeline"),
            "renderer check() should NOT be called for standard HTML elements");
    }
}
