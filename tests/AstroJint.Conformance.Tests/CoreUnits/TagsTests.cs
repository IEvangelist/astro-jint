using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/render/tags.test.ts</c>: <c>renderUniqueStylesheet()</c> renders an inline stylesheet
/// without attributes, with an escaped <c>data-vite-dev-id</c> when the stylesheet has a <c>viteDevId</c>, and without it when
/// <c>viteDevId</c> is <c>undefined</c>.
/// <para>
/// The three tests are adapted: the function is internal runtime code with no route, so the probe endpoint of the
/// <c>app-render</c> fixture (module <c>tags</c>) calls it inside the host with upstream's arguments and the tests assert the
/// exact markup that upstream asserts.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/render/tags.test.ts")]
public class TagsTests(SiteHost sites)
{
    private async Task<Probe> ProbeAsync()
    {
        var site = await sites.GetAsync(Fixture);
        return await site.ProbeAsync("tags");
    }

    [Fact(DisplayName = "renders inline style without attributes by default")]
    public async Task InlineStyle_RendersWithoutAttributesByDefault()
    {
        var probe = await ProbeAsync();

        Assert.Equal("<style>.foo { color: red; }</style>", probe.Str("renders inline style without attributes by default"));
    }

    [Fact(DisplayName = "renders inline style with data-vite-dev-id when viteDevId is provided")]
    public async Task InlineStyle_WithAViteDevId_RendersTheAttribute()
    {
        var probe = await ProbeAsync();

        Assert.Equal(
            "<style data-vite-dev-id=\"/src/components/Bar.svelte?svelte&amp;type=style&amp;lang.css\">.bar { color: blue; }</style>",
            probe.Str("renders inline style with data-vite-dev-id when viteDevId is provided"));
    }

    [Fact(DisplayName = "omits data-vite-dev-id when viteDevId is undefined")]
    public async Task InlineStyle_WithAnUndefinedViteDevId_OmitsTheAttribute()
    {
        var probe = await ProbeAsync();

        Assert.Equal("<style>.baz { color: green; }</style>", probe.Str("omits data-vite-dev-id when viteDevId is undefined"));
    }
}
