using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/render/hydration.test.ts</c>: <c>extractDirectives()</c> separates the <c>client:*</c>
/// directives and the transition attributes of a component's props (and rejects an unknown <c>client:</c> directive), and
/// <c>generateHydrateScript()</c> escapes the attribute values of an island.
/// <para>
/// All 14 tests are adapted: both functions are internal runtime code with no route, so the probe endpoint of the
/// <c>app-render</c> fixture (module <c>hydration</c>) makes upstream's calls inside the host (the same props, the same
/// renderer and island stubs) and the tests assert what upstream asserts on the results. Two differences come from the transport:
/// a probe carries JSON, so the cases that look at one key of <c>props</c> answer a boolean (<c>'client:load' in result.props</c>
/// and the like), and the symbol of the symbol test never leaves the engine (the case answers the two values read with it).
/// Upstream takes the client directives from <c>getDefaultClientDirectives()</c>, an internal module that <c>astro</c> does not
/// export; the probe uses a map with the same five names.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/render/hydration.test.ts")]
public class HydrationTests(SiteHost sites)
{
    private async Task<Probe> ProbeAsync()
    {
        var site = await sites.GetAsync(Fixture);
        return await site.ProbeAsync("hydration");
    }

    // #region extractDirectives

    [Fact(DisplayName = "returns empty hydration and all props when no client: directives present")]
    public async Task ExtractDirectives_WithoutClientDirectives_ReturnsAllPropsAndNoHydration()
    {
        var probe = await ProbeAsync();

        var value = probe["returns empty hydration and all props when no client: directives present"];
        Assert.True(value.Bool("hydrationIsNull"));
        Assert.Equal("""{"title":"Hello","count":42}""", value.GetProperty("props").Compact());
        Assert.False(value.Bool("isPage"));
    }

    [Fact(DisplayName = "extracts client:load directive")]
    public async Task ExtractDirectives_ClientLoad_IsExtracted()
    {
        var probe = await ProbeAsync();

        var value = probe["extracts client:load directive"];
        Assert.False(value.Bool("hydrationIsNull"));
        Assert.Equal("load", value.Str("directive"));
        Assert.Equal("", value.Str("value"));
        Assert.Equal("/src/Button.jsx", value.Str("componentUrl"));
        Assert.Equal("default", value.Str("componentExport"));
        Assert.Equal("""{"title":"Hello"}""", value.GetProperty("props").Compact());
    }

    [Fact(DisplayName = "extracts client:idle directive")]
    public async Task ExtractDirectives_ClientIdle_IsExtracted()
    {
        var probe = await ProbeAsync();

        Assert.Equal("idle", probe.Str("extracts client:idle directive"));
    }

    [Fact(DisplayName = "extracts client:visible directive")]
    public async Task ExtractDirectives_ClientVisible_IsExtractedWithItsOptions()
    {
        var probe = await ProbeAsync();

        var value = probe["extracts client:visible directive"];
        Assert.Equal("visible", value.Str("directive"));
        Assert.Equal("""{"rootMargin":"200px"}""", value.GetProperty("value").Compact());
    }

    [Fact(DisplayName = "extracts client:media directive with query value")]
    public async Task ExtractDirectives_ClientMedia_IsExtractedWithItsQuery()
    {
        var probe = await ProbeAsync();

        var value = probe["extracts client:media directive with query value"];
        Assert.Equal("media", value.Str("directive"));
        Assert.Equal("(max-width: 768px)", value.Str("value"));
    }

    [Fact(DisplayName = "extracts client:only directive and sets componentUrl")]
    public async Task ExtractDirectives_ClientOnly_IsExtractedWithItsRenderer()
    {
        var probe = await ProbeAsync();

        var value = probe["extracts client:only directive and sets componentUrl"];
        Assert.Equal("only", value.Str("directive"));
        Assert.Equal("react", value.Str("value"));
    }

    [Fact(DisplayName = "separates client: props from regular props")]
    public async Task ExtractDirectives_SeparatesClientPropsFromRegularProps()
    {
        var probe = await ProbeAsync();

        var value = probe["separates client: props from regular props"];
        Assert.Equal("""{"class":"btn","disabled":true}""", value.GetProperty("props").Compact());
        Assert.False(value.Bool("hasClientLoad"));
    }

    [Fact(DisplayName = "sets isPage = true when server:root is present")]
    public async Task ExtractDirectives_ServerRoot_SetsIsPage()
    {
        var probe = await ProbeAsync();

        Assert.True(probe.Bool("sets isPage = true when server:root is present"));
    }

    [Fact(DisplayName = "handles data-astro-transition-scope: included in props but excluded from propsWithoutTransitionAttributes")]
    public async Task ExtractDirectives_TransitionScope_IsOnlyInProps()
    {
        var probe = await ProbeAsync();

        var value = probe["handles data-astro-transition-scope: included in props but excluded from propsWithoutTransitionAttributes"];
        Assert.True(value.Bool("inProps"));
        Assert.False(value.Bool("inPropsWithout"));
        Assert.True(value.Bool("titleInPropsWithout"));
    }

    [Fact(DisplayName = "handles data-astro-transition-persist: excluded from propsWithoutTransitionAttributes")]
    public async Task ExtractDirectives_TransitionPersist_IsOnlyInProps()
    {
        var probe = await ProbeAsync();

        var value = probe["handles data-astro-transition-persist: excluded from propsWithoutTransitionAttributes"];
        Assert.True(value.Bool("inProps"));
        Assert.False(value.Bool("inPropsWithout"));
    }

    [Fact(DisplayName = "copies symbol props to both props and propsWithoutTransitionAttributes")]
    public async Task ExtractDirectives_SymbolProps_AreCopiedToBothObjects()
    {
        var probe = await ProbeAsync();

        var value = probe["copies symbol props to both props and propsWithoutTransitionAttributes"];
        Assert.Equal("symbol-value", value.Str("props"));
        Assert.Equal("symbol-value", value.Str("propsWithoutTransitionAttributes"));
    }

    [Fact(DisplayName = "throws for an invalid hydration directive")]
    public async Task ExtractDirectives_UnknownClientDirective_Throws()
    {
        var probe = await ProbeAsync();

        var value = probe["throws for an invalid hydration directive"];
        Assert.True(value.Bool("threw"));
        var message = value.Str("message");
        Assert.Contains("invalid hydration directive", message, StringComparison.Ordinal);
        Assert.Contains("client:unknown", message, StringComparison.Ordinal);
    }

    // #endregion

    // #region generateHydrateScript

    [Fact(DisplayName = "escapes transition directive values before rendering island attributes")]
    public async Task GenerateHydrateScript_EscapesTransitionDirectiveValues()
    {
        var probe = await ProbeAsync();

        var value = probe["escapes transition directive values before rendering island attributes"];
        var html = value.Str("html");
        Assert.Contains(
            "data-astro-transition-persist=\"&quot;&gt;&lt;img src=x onerror=alert(1)&gt;\"",
            html,
            StringComparison.Ordinal);
        Assert.DoesNotContain($"data-astro-transition-persist=\"{value.Str("payload")}\"", html, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "coerces renderer attributes before escaping them")]
    public async Task GenerateHydrateScript_CoercesRendererAttributesOnce()
    {
        var probe = await ProbeAsync();

        var value = probe["coerces renderer attributes before escaping them"];
        var html = value.Str("html");
        Assert.Equal(1, value.GetProperty("coercions").GetInt32());
        Assert.Contains("data-renderer=\"safe\"", html, StringComparison.Ordinal);
        Assert.DoesNotContain(value.Str("payload"), html, StringComparison.Ordinal);
    }

    // #endregion
}
