using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/render/sync-expansion.test.ts</c>: a script carried by a slot that was rendered to a
/// string is kept when a sync component puts that string in its template, a template renders every value except <c>null</c>,
/// <c>undefined</c> and <c>false</c> (<c>NaN</c>, <c>0n</c>, <c>0</c> and <c>''</c> too), and a template whose promise rejected
/// rethrows when it is rendered a second time.
/// <para>
/// The three tests are adapted: upstream renders its components with the Container API (<c>experimental_AstroContainer</c>), which
/// is not what the host runs; here the components are real pages of the <c>app-render</c> fixture. The first one is the TypeScript
/// module <c>lib/sync-expansion.ts</c> (the same <c>createComponent()</c> calls, <c>renderSlotToString()</c> and a script render
/// instruction) behind <c>pages/sync-expansion/script.astro</c>; the second is a partial page (<c>export const partial = true</c>,
/// so that nothing but the six paragraphs is rendered) with the six values as expressions, and the body is compared exactly; the
/// third keeps the rejecting template in a module variable and the page returns it, so every request after the first one
/// renders the same template again, and the page of that test is requested twice on a host with one engine (so that both
/// requests run the same module state): upstream's two rejections become two <c>500</c> responses whose custom 500 page
/// (<c>pages/500.astro</c>, which shows this error) contains <c>boom</c>.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/render/sync-expansion.test.ts")]
public class SyncExpansionTests(SiteHost sites)
{
    [Fact(DisplayName = "keeps scripts carried alongside a slot rendered into a sync component")]
    public async Task ScriptsCarriedByASlot_AreKeptInASyncComponent()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/sync-expansion/script");

        Assert.Equal(200, response.StatusCode);
        var html = response.Text;
        Assert.Contains("<p>slot</p>", html, StringComparison.Ordinal); // slot content
        Assert.Contains("/* island */", html, StringComparison.Ordinal); // script carried by the slot
    }

    [Fact(DisplayName = "skips only null, undefined and false")]
    public async Task Template_SkipsOnlyNullUndefinedAndFalse()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/sync-expansion/falsy");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("<p>NaN</p><p>0</p><p>0</p><p></p><p></p><p></p>", response.Text);
    }

    [Fact(DisplayName = "rethrows when the same template is rendered twice")]
    public async Task Template_RenderedTwice_RethrowsTheRejection()
    {
        var site = await sites.GetAsync(Fixture, variant: "single-engine", configure: options => options.PoolSize = 1);

        var first = await site.SendRawAsync("/sync-expansion/twice");
        var second = await site.SendRawAsync("/sync-expansion/twice");

        Assert.Equal(500, first.StatusCode);
        Assert.Contains("boom", first.Text, StringComparison.Ordinal);
        Assert.Equal(500, second.StatusCode);
        Assert.Contains("boom", second.Text, StringComparison.Ordinal);
    }
}
