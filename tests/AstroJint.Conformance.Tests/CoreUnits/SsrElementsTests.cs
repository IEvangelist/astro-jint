using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/render/ssr-elements.test.ts</c>: the functions that build the <c>&lt;link&gt;</c> and
/// <c>&lt;script&gt;</c> elements of a page from its stylesheets and module scripts: <c>createAssetLink()</c> (base path,
/// <c>assetsPrefix</c> as a string or per file type, query parameters, hash fragment), <c>createStylesheetElementSet()</c> and
/// <c>createModuleScriptElement()</c>.
/// <para>
/// All 20 tests are adapted: the functions are internal (<c>astro</c> does not export <c>core/render/ssr-element.js</c>), so the
/// probe endpoint of the <c>app-render</c> fixture (module <c>ssr-elements</c>) imports that file by its path inside the
/// installed package, and the bundle of the host runs the very code that builds the elements of every page. The probe makes
/// upstream's calls with upstream's arguments and the tests assert what upstream asserts (the elements are plain objects, so
/// the probe answers the members that a test reads: for the inline style <c>children</c> and <c>props</c>, for the module scripts
/// <c>type</c>, <c>src</c> and <c>children</c>, and so on). A page of a real build, whose <c>assetsPrefix</c> is fixed when it is
/// built, is covered by the build fixtures of the other conformance classes; these tests cover every combination upstream lists.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/render/ssr-elements.test.ts")]
public class SsrElementsTests(SiteHost sites)
{
    private async Task<Probe> ProbeAsync()
    {
        var site = await sites.GetAsync(Fixture);
        return await site.ProbeAsync("ssr-elements");
    }

    // #region createAssetLink

    [Fact(DisplayName = "returns href unchanged when no base or prefix")]
    public async Task CreateAssetLink_WithoutBaseOrPrefix_ReturnsTheHrefUnchanged()
    {
        var probe = await ProbeAsync();

        Assert.Equal("/_astro/style.css", probe.Str("returns href unchanged when no base or prefix"));
    }

    [Fact(DisplayName = "prepends base path")]
    public async Task CreateAssetLink_WithABase_PrependsIt()
    {
        var probe = await ProbeAsync();

        Assert.Equal("/docs/_astro/style.css", probe.Str("prepends base path"));
    }

    [Fact(DisplayName = "prepends string assetsPrefix")]
    public async Task CreateAssetLink_WithAStringPrefix_PrependsIt()
    {
        var probe = await ProbeAsync();

        Assert.Equal("https://cdn.com/_astro/style.css", probe.Str("prepends string assetsPrefix"));
    }

    [Fact(DisplayName = "uses css-specific prefix from per-type object")]
    public async Task CreateAssetLink_WithAPerTypePrefix_UsesTheCssPrefixForCss()
    {
        var probe = await ProbeAsync();

        Assert.StartsWith("https://css.cdn.com", probe.Str("uses css-specific prefix from per-type object"), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "falls back to fallback prefix for unknown extensions")]
    public async Task CreateAssetLink_WithAPerTypePrefix_UsesTheFallbackForOtherExtensions()
    {
        var probe = await ProbeAsync();

        Assert.StartsWith("https://cdn.com", probe.Str("falls back to fallback prefix for unknown extensions"), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "uses js-specific prefix for .js files")]
    public async Task CreateAssetLink_WithAPerTypePrefix_UsesTheJsPrefixForJs()
    {
        var probe = await ProbeAsync();

        Assert.StartsWith("https://js.cdn.com", probe.Str("uses js-specific prefix for .js files"), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "appends query params")]
    public async Task CreateAssetLink_WithQueryParams_AppendsThem()
    {
        var probe = await ProbeAsync();

        Assert.Equal("/_astro/style.css?v=123", probe.Str("appends query params"));
    }

    [Fact(DisplayName = "preserves hash fragment and appends query params before it")]
    public async Task CreateAssetLink_WithAHashAndQueryParams_PutsTheQueryBeforeTheHash()
    {
        var probe = await ProbeAsync();

        var result = probe.Str("preserves hash fragment and appends query params before it");
        Assert.Contains("?v=1", result, StringComparison.Ordinal); // query param should be present
        Assert.EndsWith("#inline", result, StringComparison.Ordinal); // hash should be at end
        Assert.True(result.IndexOf('?', StringComparison.Ordinal) < result.IndexOf('#', StringComparison.Ordinal), "query before hash");
    }

    [Fact(DisplayName = "preserves hash fragment without query params")]
    public async Task CreateAssetLink_WithAHashAndNoQueryParams_KeepsTheHash()
    {
        var probe = await ProbeAsync();

        Assert.EndsWith("#inline", probe.Str("preserves hash fragment without query params"), StringComparison.Ordinal);
    }

    // #endregion

    // #region createStylesheetElementSet

    [Fact(DisplayName = "creates an inline style element")]
    public async Task CreateStylesheetElementSet_InlineStyle_CreatesAStyleElement()
    {
        var probe = await ProbeAsync();

        var value = probe["creates an inline style element"];
        Assert.Equal("body{color:red}", value.Str("children"));
        Assert.Equal("{}", value.GetProperty("props").Compact());
    }

    [Fact(DisplayName = "creates an external link element with rel=stylesheet")]
    public async Task CreateStylesheetElementSet_ExternalStyle_CreatesALinkElement()
    {
        var probe = await ProbeAsync();

        var value = probe["creates an external link element with rel=stylesheet"];
        Assert.Equal("stylesheet", value.Str("rel"));
        Assert.Equal("", value.Str("children"));
    }

    [Fact(DisplayName = "applies base path to external stylesheet href")]
    public async Task CreateStylesheetElementSet_WithABase_PrefixesTheHref()
    {
        var probe = await ProbeAsync();

        // expected /docs/ prefix
        Assert.StartsWith("/docs/", probe.Str("applies base path to external stylesheet href"), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "applies assetsPrefix to external stylesheet href \u2014 Migrated from astro-assets-prefix.test.js")]
    public async Task CreateStylesheetElementSet_WithAnAssetsPrefix_PrefixesTheHref()
    {
        var probe = await ProbeAsync();

        // expected cdn prefix
        Assert.StartsWith(
            "https://cdn.com",
            probe.Str("applies assetsPrefix to external stylesheet href \u2014 Migrated from astro-assets-prefix.test.js"),
            StringComparison.Ordinal);
    }

    [Fact(DisplayName = "applies css-specific prefix \u2014 Migrated from astro-assets-prefix-multi-cdn.test.js")]
    public async Task CreateStylesheetElementSet_WithACssPrefix_PrefixesTheHref()
    {
        var probe = await ProbeAsync();

        // expected css cdn prefix
        Assert.StartsWith(
            "https://css.cdn.com",
            probe.Str("applies css-specific prefix \u2014 Migrated from astro-assets-prefix-multi-cdn.test.js"),
            StringComparison.Ordinal);
    }

    [Fact(DisplayName = "handles mixed inline and external stylesheets")]
    public async Task CreateStylesheetElementSet_MixedStylesheets_CreatesTwoElements()
    {
        var probe = await ProbeAsync();

        Assert.Equal(2, probe["handles mixed inline and external stylesheets"].GetInt32());
    }

    // #endregion

    // #region createModuleScriptElement

    [Fact(DisplayName = "creates an external module script with type=module and src")]
    public async Task CreateModuleScriptElement_External_HasTypeAndSrc()
    {
        var probe = await ProbeAsync();

        var value = probe["creates an external module script with type=module and src"];
        Assert.Equal("module", value.Str("type"));
        Assert.Equal("/_astro/app.js", value.Str("src"));
        Assert.Equal("", value.Str("children"));
    }

    [Fact(DisplayName = "creates an inline module script with type=module and children")]
    public async Task CreateModuleScriptElement_Inline_HasTypeAndChildren()
    {
        var probe = await ProbeAsync();

        var value = probe["creates an inline module script with type=module and children"];
        Assert.Equal("module", value.Str("type"));
        Assert.Equal("console.log(1)", value.Str("children"));
        Assert.True(value.Bool("srcIsUndefined"));
    }

    [Fact(DisplayName = "applies string assetsPrefix to external script src \u2014 Migrated from astro-assets-prefix.test.js")]
    public async Task CreateModuleScriptElement_WithAStringPrefix_PrefixesTheSrc()
    {
        var probe = await ProbeAsync();

        // expected cdn prefix
        Assert.StartsWith(
            "https://cdn.com",
            probe.Str("applies string assetsPrefix to external script src \u2014 Migrated from astro-assets-prefix.test.js"),
            StringComparison.Ordinal);
    }

    [Fact(DisplayName = "applies js-specific prefix to external script src \u2014 Migrated from astro-assets-prefix-multi-cdn.test.js")]
    public async Task CreateModuleScriptElement_WithAJsPrefix_PrefixesTheSrc()
    {
        var probe = await ProbeAsync();

        // expected js cdn prefix
        Assert.StartsWith(
            "https://js.cdn.com",
            probe.Str("applies js-specific prefix to external script src \u2014 Migrated from astro-assets-prefix-multi-cdn.test.js"),
            StringComparison.Ordinal);
    }

    [Fact(DisplayName = "applies base path to external script src")]
    public async Task CreateModuleScriptElement_WithABase_PrefixesTheSrc()
    {
        var probe = await ProbeAsync();

        Assert.StartsWith("/docs/", probe.Str("applies base path to external script src"), StringComparison.Ordinal);
    }

    // #endregion
}
