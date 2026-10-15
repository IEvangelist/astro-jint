using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/render/rendering.test.ts</c>: how the runtime evaluates and renders a tree of
/// components (depth-first for synchronous children, deferred for async ones, eager for adjacent async siblings), that a
/// synchronous tree needs no promise, that blank fragments are skipped, that every kind of child (function, promise, array,
/// generators) is rendered in order, and that a thenable with a <c>render()</c> method is awaited.
/// <para>
/// All 7 tests are adapted: they call the <c>render()</c> methods of the instances that <c>createComponent()</c> returns, with no
/// route involved. The probe endpoint of the <c>app-render</c> fixture (<c>src/pages/probe/[name].ts</c>, module
/// <c>rendering</c>) makes the same calls, with the same components and arguments, inside the host and answers with the lists
/// of evaluated and rendered ids; the tests assert upstream's lists, so they prove that the render pipeline of Astro evaluates
/// and writes in the same order inside Jint (timers included: the async cases use <c>setTimeout</c>).
/// </para>
/// </summary>
[Trait("Upstream", "core-units/render/rendering.test.ts")]
public class RenderingTests(SiteHost sites)
{
    private async Task<Probe> ProbeAsync()
    {
        var site = await sites.GetAsync(Fixture);
        return await site.ProbeAsync("rendering");
    }

    [Fact(DisplayName = "components are evaluated and rendered depth-first")]
    public async Task Components_AreEvaluatedAndRenderedDepthFirst()
    {
        var probe = await ProbeAsync();

        var value = probe["components are evaluated and rendered depth-first"];
        string[] expected = ["root", "root/scalar_1", "root/nested", "root/nested/scalar", "root/scalar_2"];
        Assert.Equal(expected, value.Strings("evaluated"));
        Assert.Equal(expected, value.Strings("rendered"));
    }

    [Fact(DisplayName = "synchronous component trees are rendered without promises")]
    public async Task SynchronousComponentTrees_AreRenderedWithoutPromises()
    {
        var probe = await ProbeAsync();

        var value = probe["synchronous component trees are rendered without promises"];
        Assert.False(value.Bool("isPromise"));
        string[] expected = ["root", "root/scalar_1", "root/scalar_2", "root/scalar_3", "root/scalar_4"];
        Assert.Equal(expected, value.Strings("evaluated"));
        Assert.Equal(expected, value.Strings("rendered"));
    }

    [Fact(DisplayName = "async component children are deferred")]
    public async Task AsyncComponentChildren_AreDeferred()
    {
        var probe = await ProbeAsync();

        var value = probe["async component children are deferred"];
        Assert.Equal(["root", "root/asyncnested", "root/scalar", "root/asyncnested/scalar"], value.Strings("evaluated"));
        Assert.Equal(["root", "root/asyncnested", "root/asyncnested/scalar", "root/scalar"], value.Strings("rendered"));
    }

    [Fact(DisplayName = "adjacent async components are evaluated eagerly")]
    public async Task AdjacentAsyncComponents_AreEvaluatedEagerly()
    {
        var probe = await ProbeAsync();

        var value = probe["adjacent async components are evaluated eagerly"];
        Assert.Equal(["root", "root/asyncnested_1", "root/asyncnested_2"], value.Strings("evaluatedBeforeRelease"));
        Assert.Equal(
            ["root", "root/asyncnested_1", "root/asyncnested_2", "root/asyncnested_1/scalar", "root/asyncnested_2/scalar"],
            value.Strings("evaluatedAfterRelease"));
        Assert.Equal(
            ["root", "root/asyncnested_1", "root/asyncnested_1/scalar", "root/asyncnested_2", "root/asyncnested_2/scalar"],
            value.Strings("rendered"));
    }

    [Fact(DisplayName = "skip rendering blank html fragments")]
    public async Task BlankHtmlFragments_AreSkipped()
    {
        var probe = await ProbeAsync();

        var chunk = Assert.Single(probe["skip rendering blank html fragments"].EnumerateArray());
        Assert.True(chunk.Bool("isHTMLString"));
        Assert.Equal("hello world", chunk.Str("text"));
    }

    [Fact(DisplayName = "all primitives are rendered in order")]
    public async Task AllPrimitives_AreRenderedInOrder()
    {
        var probe = await ProbeAsync();

        Assert.Equal(
            [
                "root",
                "root/first",
                "root/func",
                "root/promise",
                "root/array_func",
                "root/array_scalar",
                "root/async_generator",
                "root/generator",
                "root/last",
            ],
            probe["all primitives are rendered in order"].Strings("rendered"));
    }

    [Fact(DisplayName = "awaits thenables that also have a render method")]
    public async Task Thenable_WithARenderMethod_IsAwaited()
    {
        var probe = await ProbeAsync();

        Assert.Equal("expected", probe.Str("awaits thenables that also have a render method"));
    }
}
