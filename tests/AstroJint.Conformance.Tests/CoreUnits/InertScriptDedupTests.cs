using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/render/inert-script-dedup.test.ts</c>: the render instructions for a client directive, a
/// renderer's hydration script and the server island runtime, rendered inside a <c>&lt;template&gt;</c> (template depth 1), are inert:
/// they emit their scripts but do not mark them as emitted, so the same instruction rendered outside a template still emits
/// them and consumes the deduplication; an instruction that was already deduplicated emits nothing.
/// <para>
/// The four tests are adapted. There is no HTTP-visible route for a hand-made render result (upstream passes a stub
/// <c>SSRResult</c> to <c>chunkToString()</c>), so the probe endpoint of the <c>app-render</c> fixture (module
/// <c>inert-script-dedup</c>) makes upstream's calls inside the host (the same stub result, the same instructions, the same two
/// template depths) and the tests assert what upstream's assertions look at: the output of each call and the dedup state after it.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/render/inert-script-dedup.test.ts")]
public class InertScriptDedupTests(SiteHost sites)
{
    private async Task<Probe> ProbeAsync()
    {
        var site = await sites.GetAsync(Fixture);
        return await site.ProbeAsync("inert-script-dedup");
    }

    [Fact(DisplayName = "does not consume directive or hydration dedup inside templates")]
    public async Task Directive_InsideATemplate_DoesNotConsumeTheDedup()
    {
        var probe = await ProbeAsync();

        var value = probe["does not consume directive or hydration dedup inside templates"];
        var inert = value.GetProperty("inert");
        Assert.Matches("<script>", inert.Str("output"));
        Assert.False(inert.Bool("hasHydrationScript"));
        Assert.False(inert.Bool("hasLoad"));

        var executable = value.GetProperty("executable");
        Assert.Matches("<script>", executable.Str("output"));
        Assert.True(executable.Bool("hasHydrationScript"));
        Assert.True(executable.Bool("hasLoad"));
    }

    [Fact(DisplayName = "does not emit inert directive scripts when already deduplicated")]
    public async Task Directive_AlreadyDeduplicated_EmitsNothingInATemplate()
    {
        var probe = await ProbeAsync();

        Assert.Equal("", probe.Str("does not emit inert directive scripts when already deduplicated"));
    }

    [Fact(DisplayName = "does not consume renderer hydration dedup inside templates")]
    public async Task RendererHydrationScript_InsideATemplate_DoesNotConsumeTheDedup()
    {
        var probe = await ProbeAsync();

        var value = probe["does not consume renderer hydration dedup inside templates"];
        var inert = value.GetProperty("inert");
        Assert.Matches("__react", inert.Str("output"));
        Assert.False(inert.Bool("consumed"));

        var executable = value.GetProperty("executable");
        Assert.Matches("__react", executable.Str("output"));
        Assert.True(executable.Bool("consumed"));
    }

    [Fact(DisplayName = "does not consume server-island runtime dedup inside templates")]
    public async Task ServerIslandRuntime_InsideATemplate_DoesNotConsumeTheDedup()
    {
        var probe = await ProbeAsync();

        var value = probe["does not consume server-island runtime dedup inside templates"];
        var inert = value.GetProperty("inert");
        Assert.Matches("replaceServerIsland", inert.Str("output"));
        Assert.False(inert.Bool("consumed"));

        var executable = value.GetProperty("executable");
        Assert.Matches("replaceServerIsland", executable.Str("output"));
        Assert.True(executable.Bool("consumed"));
    }
}
