using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/render/head.test.ts</c>: the render instructions of the runtime cannot be mutated through
/// the objects that earlier calls returned, and the stylesheet link of a page lands in the <c>&lt;head&gt;</c> through three
/// levels of layouts, whether the base layout writes a <c>&lt;head&gt;</c> (explicit), lets the head slot sit directly in the
/// <c>&lt;html&gt;</c> (implicit) or has no document at all (bare).
/// <para>
/// 'isolates mutations to render instructions' is adapted: it calls <c>renderHead()</c>, <c>maybeRenderHead()</c>,
/// <c>templateEnter()</c> and <c>templateExit()</c> and has no route, so the probe endpoint of the <c>app-render</c> fixture
/// (module <c>head</c>) makes the same calls inside the host and the test asserts that the types equal upstream's.
/// </para>
/// <para>
/// The three layout tests are adapted. Upstream stubs the <c>headElements</c> of the environment so that every render injects
/// one <c>&lt;link rel="stylesheet" href="/main.css"&gt;</c>; no host exposes that hook. What produces a stylesheet link in a real
/// build is a <c>&lt;style&gt;</c> of the page (the fixture builds with <c>inlineStylesheets: 'never'</c>), so the three pages
/// <c>pages/head/{explicit,implicit,bare}.astro</c> have a scoped style and the three layouts of the upstream test are the
/// <c>.astro</c> components <c>components/head/*</c> (a base layout with a <c>head</c> slot inside <c>&lt;head&gt;</c>, one with the
/// slot directly inside <c>&lt;html&gt;</c>, one that only has a <c>&lt;slot /&gt;</c>), each wrapped by a page layout that
/// forwards its slots. The assertions are upstream's: exactly one link, in the head and not in the body (for the bare layout:
/// exactly one link).
/// </para>
/// </summary>
[Trait("Upstream", "core-units/render/head.test.ts")]
public class HeadTests(SiteHost sites)
{
    private async Task<RawResponse> GetAsync(string path)
    {
        var site = await sites.GetAsync(Fixture);
        return await site.SendRawAsync(path);
    }

    [Fact(DisplayName = "isolates mutations to render instructions")]
    public async Task RenderInstructions_AreIsolatedFromMutations()
    {
        var site = await sites.GetAsync(Fixture);
        var probe = await site.ProbeAsync("head");

        var value = probe["isolates mutations to render instructions"];
        string[] types = ["head", "maybe-head", "template-enter", "template-exit"];
        Assert.Equal(types, value.Strings("expected"));
        Assert.Equal(types, value.Strings("actual"));
    }

    // #region Injected head contents

    [Fact(DisplayName = "Multi-level layouts and head injection, with explicit head")]
    public async Task HeadInjection_WithExplicitHead_PutsTheLinkInTheHead()
    {
        var response = await GetAsync("/head/explicit");

        Assert.Equal(200, response.StatusCode);
        var html = Html(response);
        Assert.Single(html.QuerySelectorAll("head link"));
        Assert.Empty(html.QuerySelectorAll("body link"));
    }

    [Fact(DisplayName = "Multi-level layouts and head injection, without explicit head")]
    public async Task HeadInjection_WithoutExplicitHead_PutsTheLinkInTheHead()
    {
        var response = await GetAsync("/head/implicit");

        Assert.Equal(200, response.StatusCode);
        var html = Html(response);
        Assert.Single(html.QuerySelectorAll("head link"));
        Assert.Empty(html.QuerySelectorAll("body link"));
    }

    [Fact(DisplayName = "Multi-level layouts and head injection, without any content in layouts")]
    public async Task HeadInjection_WithoutAnyContentInLayouts_RendersTheLinkOnce()
    {
        var response = await GetAsync("/head/bare");

        Assert.Equal(200, response.StatusCode);
        Assert.Single(Html(response).QuerySelectorAll("link"));
    }

    // #endregion
}
