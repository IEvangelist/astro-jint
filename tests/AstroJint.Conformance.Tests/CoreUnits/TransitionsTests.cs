using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/render/transitions.test.ts</c>: the helpers that generate the view-transition CSS of a
/// page (<c>reEncode</c>, <c>toTimeValue</c>, <c>stringifyAnimation</c>, <c>stringifyAnimations</c>,
/// <c>ViewTransitionStyleSheet</c> and <c>renderTransition</c>), including the escaping of CSS delimiters in animation values.
/// <para>
/// All 32 tests are adapted: the helpers are internal runtime code with no route, so the probe endpoint of the
/// <c>app-render</c> fixture (module <c>transitions</c>) makes upstream's calls inside the host with upstream's arguments and the
/// tests assert what upstream asserts on the strings that come back. The tests that assert several calls get several values
/// (the first test of <c>reEncode</c> three, 'converts positive number to "{n}ms"' and 'passes string values through unchanged'
/// two each); 'handles a single animation identical to stringifyAnimation' gets both strings and compares them here; the tests
/// of <c>renderTransition</c> get the generated <c>&lt;style&gt;</c> element (<c>result._metadata.extraHead[0]</c>) and parse it
/// as upstream does (<c>&lt;head&gt;…&lt;/head&gt;</c>). Messages that upstream attaches to an assertion are in comments.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/render/transitions.test.ts")]
public class TransitionsTests(SiteHost sites)
{
    private async Task<Probe> ProbeAsync()
    {
        var site = await sites.GetAsync(Fixture);
        return await site.ProbeAsync("transitions");
    }

    private static void AssertIncludes(string actual, string expected) => Assert.Contains(expected, actual, StringComparison.Ordinal);

    private static void AssertExcludes(string actual, string unexpected) => Assert.DoesNotContain(unexpected, actual, StringComparison.Ordinal);

    // #region reEncode

    [Fact(DisplayName = "passes through simple alphanumeric strings unchanged")]
    public async Task ReEncode_SimpleAlphanumericStrings_AreUnchanged()
    {
        var probe = await ProbeAsync();

        probe.AssertStrings("passes through simple alphanumeric strings unchanged", "hello", "my-transition", "Hero123");
    }

    [Fact(DisplayName = "prepends underscore when string starts with a digit")]
    public async Task ReEncode_StartingWithADigit_PrependsAnUnderscore()
    {
        var probe = await ProbeAsync();

        Assert.Equal("_1hero", probe.Str("prepends underscore when string starts with a digit"));
    }

    [Fact(DisplayName = "prepends underscore when string starts with a minus sign")]
    public async Task ReEncode_StartingWithAMinusSign_PrependsAnUnderscore()
    {
        var probe = await ProbeAsync();

        Assert.Equal("_-hero", probe.Str("prepends underscore when string starts with a minus sign"));
    }

    [Fact(DisplayName = "replaces spaces with their hex escape")]
    public async Task ReEncode_Spaces_AreReplacedWithTheirHexEscape()
    {
        var probe = await ProbeAsync();

        // expected space encoded as _20
        AssertIncludes(probe.Str("replaces spaces with their hex escape"), "_20");
    }

    [Fact(DisplayName = "replaces dots with their hex escape")]
    public async Task ReEncode_Dots_AreReplacedWithTheirHexEscape()
    {
        var probe = await ProbeAsync();

        // expected . encoded as _2e
        AssertIncludes(probe.Str("replaces dots with their hex escape"), "_2e");
    }

    [Fact(DisplayName = "doubles underscores to avoid collision, and prepends _ if starts with underscore")]
    public async Task ReEncode_Underscores_AreDoubledAndAPrefixIsAdded()
    {
        var probe = await ProbeAsync();

        // '_' (code 95) encodes to '__'; since '__hero' starts with '_', a further '_' is prepended
        Assert.Equal("___hero", probe.Str("doubles underscores to avoid collision, and prepends _ if starts with underscore"));
    }

    [Fact(DisplayName = "handles non-ASCII characters by copying them through")]
    public async Task ReEncode_NonAsciiCharacters_AreCopiedThrough()
    {
        var probe = await ProbeAsync();

        // expected \u00e9 to pass through
        AssertIncludes(probe.Str("handles non-ASCII characters by copying them through"), "\u00e9");
    }

    // #endregion

    // #region toTimeValue

    [Fact(DisplayName = "converts 0 to \"0ms\"")]
    public async Task ToTimeValue_Zero_IsZeroMilliseconds()
    {
        var probe = await ProbeAsync();

        Assert.Equal("0ms", probe.Str("converts 0 to \"0ms\""));
    }

    [Fact(DisplayName = "converts positive number to \"{n}ms\"")]
    public async Task ToTimeValue_PositiveNumbers_AreMilliseconds()
    {
        var probe = await ProbeAsync();

        probe.AssertStrings("converts positive number to \"{n}ms\"", "300ms", "1000ms");
    }

    [Fact(DisplayName = "passes string values through unchanged")]
    public async Task ToTimeValue_Strings_AreUnchanged()
    {
        var probe = await ProbeAsync();

        probe.AssertStrings("passes string values through unchanged", "0.3s", "300ms");
    }

    // #endregion

    // #region stringifyAnimation

    [Fact(DisplayName = "serializes animation-name")]
    public async Task StringifyAnimation_Name_IsSerialized()
    {
        var probe = await ProbeAsync();

        var result = probe.Str("serializes animation-name");
        AssertIncludes(result, "animation-name");
        AssertIncludes(result, "fade-in");
    }

    [Fact(DisplayName = "serializes duration as ms when numeric")]
    public async Task StringifyAnimation_NumericDuration_IsSerializedAsMilliseconds()
    {
        var probe = await ProbeAsync();

        var result = probe.Str("serializes duration as ms when numeric");
        AssertIncludes(result, "animation-duration");
        AssertIncludes(result, "300ms");
    }

    [Fact(DisplayName = "serializes easing, direction, delay, and fillMode")]
    public async Task StringifyAnimation_EasingDirectionDelayAndFillMode_AreSerialized()
    {
        var probe = await ProbeAsync();

        var result = probe.Str("serializes easing, direction, delay, and fillMode");
        AssertIncludes(result, "animation-timing-function");
        AssertIncludes(result, "ease-in-out");
        AssertIncludes(result, "animation-direction");
        AssertIncludes(result, "reverse");
        AssertIncludes(result, "animation-fill-mode");
        AssertIncludes(result, "both");
    }

    [Fact(DisplayName = "escapes CSS delimiters in animation values")]
    public async Task StringifyAnimation_CssDelimiters_AreEscaped()
    {
        var probe = await ProbeAsync();

        var result = probe.Str("escapes CSS delimiters in animation values");
        AssertIncludes(result, """animation-duration: 1ms\3B \7D custom\7B value;""");
        AssertExcludes(result, "}custom{");
    }

    [Fact(DisplayName = "preserves CSS functions in animation values")]
    public async Task StringifyAnimation_CssFunctions_ArePreserved()
    {
        var probe = await ProbeAsync();

        AssertIncludes(probe.Str("preserves CSS functions in animation values"), "calc(1s + var(--transition-delay, 200ms))");
    }

    [Fact(DisplayName = "accepts an array of animations")]
    public async Task StringifyAnimation_AnArrayOfAnimations_IsAccepted()
    {
        var probe = await ProbeAsync();

        var result = probe.Str("accepts an array of animations");
        AssertIncludes(result, "fade-in");
        AssertIncludes(result, "slide-in");
    }

    // #endregion

    // #region stringifyAnimations

    [Fact(DisplayName = "merges multiple animation names into one animation-name declaration")]
    public async Task StringifyAnimations_MultipleNames_AreMerged()
    {
        var probe = await ProbeAsync();

        var result = probe.Str("merges multiple animation names into one animation-name declaration");
        AssertIncludes(result, "animation-name");
        AssertIncludes(result, "fade-in");
        AssertIncludes(result, "slide-in");
    }

    [Fact(DisplayName = "merges multiple durations into one animation-duration declaration")]
    public async Task StringifyAnimations_MultipleDurations_AreMerged()
    {
        var probe = await ProbeAsync();

        var result = probe.Str("merges multiple durations into one animation-duration declaration");
        AssertIncludes(result, "animation-duration");
        AssertIncludes(result, "200ms");
        AssertIncludes(result, "400ms");
    }

    [Fact(DisplayName = "handles a single animation identical to stringifyAnimation")]
    public async Task StringifyAnimations_ASingleAnimation_EqualsStringifyAnimation()
    {
        var probe = await ProbeAsync();

        var value = probe["handles a single animation identical to stringifyAnimation"];
        Assert.Equal(value.Str("viaWrapper"), value.Str("single"));
    }

    [Fact(DisplayName = "omits animation properties that are not set")]
    public async Task StringifyAnimations_UnsetProperties_AreOmitted()
    {
        var probe = await ProbeAsync();

        var result = probe.Str("omits animation properties that are not set");
        AssertExcludes(result, "animation-duration");
        AssertExcludes(result, "animation-timing-function");
        AssertExcludes(result, "animation-direction");
        AssertExcludes(result, "animation-delay");
        AssertExcludes(result, "animation-fill-mode");
    }

    [Fact(DisplayName = "handles an empty array producing only animation-name with empty value")]
    public async Task StringifyAnimations_AnEmptyArray_HasNoAnimationName()
    {
        var probe = await ProbeAsync();

        // edge case: no animations → animation-name has no entries
        AssertExcludes(probe.Str("handles an empty array producing only animation-name with empty value"), "animation-name");
    }

    // #endregion

    // #region ViewTransitionStyleSheet

    [Fact(DisplayName = "toString() includes view-transition-name declaration")]
    public async Task ViewTransitionStyleSheet_ToString_IncludesTheViewTransitionName()
    {
        var probe = await ProbeAsync();

        AssertIncludes(
            probe.Str("toString() includes view-transition-name declaration"),
            "[data-astro-transition-scope=\"astro-abc-1\"] { view-transition-name: hero; }");
    }

    [Fact(DisplayName = "addModern() adds a modern ::view-transition rule inside @layer")]
    public async Task ViewTransitionStyleSheet_AddModern_AddsARuleInsideTheLayer()
    {
        var probe = await ProbeAsync();

        var css = probe.Str("addModern() adds a modern ::view-transition rule inside @layer");
        AssertIncludes(css, "@layer astro"); // expected @layer astro
        AssertIncludes(css, "::view-transition-old(hero)"); // expected old selector
        AssertIncludes(css, "animation: fade-out 300ms;");
    }

    [Fact(DisplayName = "addFallback() adds fallback selectors outside @layer")]
    public async Task ViewTransitionStyleSheet_AddFallback_AddsSelectorsOutsideTheLayer()
    {
        var probe = await ProbeAsync();

        var css = probe.Str("addFallback() adds fallback selectors outside @layer");
        AssertIncludes(css, "[data-astro-transition-fallback=\"old\"]"); // expected fallback selector
        AssertIncludes(css, "[data-astro-transition-scope=\"astro-abc-1\"]");
    }

    [Fact(DisplayName = "addAnimationPair() adds both modern and fallback rules for forwards direction")]
    public async Task ViewTransitionStyleSheet_AddAnimationPair_Forwards_AddsModernAndFallbackRules()
    {
        var probe = await ProbeAsync();

        var css = probe.Str("addAnimationPair() adds both modern and fallback rules for forwards direction");
        AssertIncludes(css, "::view-transition-old(banner)");
        AssertIncludes(css, "[data-astro-transition-fallback=\"old\"]");
    }

    [Fact(DisplayName = "addAnimationPair() adds [data-astro-transition=back] prefix for backwards direction")]
    public async Task ViewTransitionStyleSheet_AddAnimationPair_Backwards_AddsTheBackPrefix()
    {
        var probe = await ProbeAsync();

        AssertIncludes(
            probe.Str("addAnimationPair() adds [data-astro-transition=back] prefix for backwards direction"),
            "[data-astro-transition=back]::view-transition-old(banner)");
    }

    [Fact(DisplayName = "addAnimationPair() quotes custom direction names in the selector")]
    public async Task ViewTransitionStyleSheet_AddAnimationPair_QuotesCustomDirectionNames()
    {
        var probe = await ProbeAsync();

        // expected quoted custom direction prefix
        AssertIncludes(
            probe.Str("addAnimationPair() quotes custom direction names in the selector"),
            "[data-astro-transition=\"downward\"]::view-transition-old(banner)");
    }

    [Fact(DisplayName = "addAnimationPair() supports custom direction names that are not CSS identifiers")]
    public async Task ViewTransitionStyleSheet_AddAnimationPair_SupportsDirectionNamesThatAreNotIdentifiers()
    {
        var probe = await ProbeAsync();

        var css = probe.Str("addAnimationPair() supports custom direction names that are not CSS identifiers");
        // expected quoted custom direction prefix
        AssertIncludes(css, "[data-astro-transition=\"2nd step\"]::view-transition-new(banner)");
        // expected punctuation in custom direction to be escaped
        AssertIncludes(css, """[data-astro-transition="a\"b]c{d}"]::view-transition-old(banner)""");
    }

    [Fact(DisplayName = "addAnimationRaw() adds same rule to both modern and fallback")]
    public async Task ViewTransitionStyleSheet_AddAnimationRaw_AddsTheRuleToModernAndFallback()
    {
        var probe = await ProbeAsync();

        var css = probe.Str("addAnimationRaw() adds same rule to both modern and fallback");
        AssertIncludes(css, "::view-transition-new(hero)"); // expected modern rule
        AssertIncludes(css, "[data-astro-transition-fallback=\"new\"]"); // expected fallback rule
    }

    [Fact(DisplayName = "produces empty @layer block when no modern rules added")]
    public async Task ViewTransitionStyleSheet_WithoutModernRules_HasNoLayer()
    {
        var probe = await ProbeAsync();

        // should not include @layer when no modern rules
        AssertExcludes(probe.Str("produces empty @layer block when no modern rules added"), "@layer");
    }

    // #endregion

    // #region renderTransition

    [Fact(DisplayName = "keeps animation values inside the generated style element")]
    public async Task RenderTransition_AnimationValues_StayInsideTheStyleElement()
    {
        var probe = await ProbeAsync();

        var style = probe.Str("keeps animation values inside the generated style element");
        var html = ConformanceSite.ParseHtml($"<head>{style}</head>");
        Assert.Single(html.QuerySelectorAll("head style"));
        Assert.Empty(html.QuerySelectorAll("head script"));
        AssertIncludes(style, """\3C /style>\3C script>test\3C /script>""");
    }

    [Fact(DisplayName = "keeps animation values inside their generated CSS declarations")]
    public async Task RenderTransition_AnimationValues_StayInsideTheirDeclarations()
    {
        var probe = await ProbeAsync();

        var style = probe.Str("keeps animation values inside their generated CSS declarations");
        AssertIncludes(style, """animation-duration: 1ms\3B \7D custom\7B value;""");
        AssertExcludes(style, "}custom{");
    }

    // #endregion
}
