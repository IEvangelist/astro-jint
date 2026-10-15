using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/render/class-list-and-style.test.ts</c>: <c>class:list</c> and <c>style</c> values
/// (strings, arrays, objects, falsy values) become the <c>class</c> and <c>style</c> attributes that Astro's runtime writes,
/// by themselves (<c>addAttribute()</c>, <c>toStyleString()</c>) and when they are forwarded to a component that spreads its
/// props on a <c>&lt;span&gt;</c>.
/// <para>
/// The two forwarding tests are ported: the pages <c>class-list/forwarded</c> and <c>class-list/style-forwarded</c> of the
/// <c>app-render</c> fixture are the components that upstream builds with <c>createMultiChildPage()</c> and
/// <c>spreadPropsSpan</c> (the <c>.astro</c> component <c>SpreadPropsSpan</c> is <c>&lt;span {...Astro.props}&gt;{Astro.props.class
/// ?? ''}&lt;/span&gt;</c>), and the assertions are upstream's. The other 23 tests call the functions of
/// <c>astro/runtime/server</c> and have no route; they are adapted: the probe endpoint of the fixture
/// (<c>src/pages/probe/[name].ts</c>, module <c>class-list-and-style</c>) makes the same calls with the same arguments inside
/// the host and answers with what they returned, and the tests assert upstream's expected values on that answer, so they prove
/// that these functions behave the same inside Jint.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/render/class-list-and-style.test.ts")]
public class ClassListAndStyleTests(SiteHost sites)
{
    // The classes of the spans of the page, which has an eighth span without class.
    private static readonly string[] ForwardedClasses =
    [
        "test control",
        "test expression",
        "test true",
        "test truthy",
        "test set",
        "hello goodbye hello world hello friend",
        "foo baz",
    ];

    private async Task<Probe> ProbeAsync()
    {
        var site = await sites.GetAsync(Fixture);
        return await site.ProbeAsync("class-list-and-style");
    }

    // #region class:list

    [Fact(DisplayName = "handles a plain string")]
    public async Task ClassList_PlainString_IsTheClass()
    {
        var probe = await ProbeAsync();

        Assert.Equal(" class=\"test expression\"", probe.Str("handles a plain string"));
    }

    [Fact(DisplayName = "handles an array of strings")]
    public async Task ClassList_Array_IsJoined()
    {
        var probe = await ProbeAsync();

        Assert.Equal(" class=\"test set\"", probe.Str("handles an array of strings"));
    }

    [Fact(DisplayName = "handles an object with boolean values")]
    public async Task ClassList_ObjectWithBooleans_KeepsTheTrueKeys()
    {
        var probe = await ProbeAsync();

        Assert.Equal(" class=\"test true\"", probe.Str("handles an object with boolean values"));
    }

    [Fact(DisplayName = "handles an object with truthy/falsy values")]
    public async Task ClassList_ObjectWithTruthyAndFalsyValues_KeepsTheTruthyKeys()
    {
        var probe = await ProbeAsync();

        Assert.Equal(" class=\"test truthy\"", probe.Str("handles an object with truthy/falsy values"));
    }

    [Fact(DisplayName = "handles nested arrays and objects")]
    public async Task ClassList_NestedArraysAndObjects_AreFlattened()
    {
        var probe = await ProbeAsync();

        Assert.Equal(" class=\"hello goodbye hello world hello friend\"", probe.Str("handles nested arrays and objects"));
    }

    [Fact(DisplayName = "handles conditional expressions in arrays")]
    public async Task ClassList_ConditionalExpressionsInArrays_DropTheFalseOnes()
    {
        var probe = await ProbeAsync();

        Assert.Equal(" class=\"foo baz\"", probe.Str("handles conditional expressions in arrays"));
    }

    [Fact(DisplayName = "returns empty string when all values are falsy")]
    public async Task ClassList_AllValuesFalsy_IsEmpty()
    {
        var probe = await ProbeAsync();

        Assert.Equal("", probe.Str("returns empty string when all values are falsy"));
    }

    [Fact(DisplayName = "returns empty string for null")]
    public async Task ClassList_Null_IsEmpty()
    {
        var probe = await ProbeAsync();

        Assert.Equal("", probe.Str("returns empty string for null"));
    }

    [Fact(DisplayName = "returns empty string for undefined")]
    public async Task ClassList_Undefined_IsEmpty()
    {
        var probe = await ProbeAsync();

        Assert.Equal("", probe.Str("returns empty string for undefined"));
    }

    // #endregion

    // #region toStyleString

    [Fact(DisplayName = "converts camelCase to kebab-case")]
    public async Task ToStyleString_CamelCase_BecomesKebabCase()
    {
        var probe = await ProbeAsync();

        Assert.Equal("background-color:green", probe.Str("converts camelCase to kebab-case"));
    }

    [Fact(DisplayName = "preserves CSS custom properties")]
    public async Task ToStyleString_CustomProperties_AreKept()
    {
        var probe = await ProbeAsync();

        Assert.Equal("--my-var:red", probe.Str("preserves CSS custom properties"));
    }

    [Fact(DisplayName = "joins multiple properties with semicolons")]
    public async Task ToStyleString_MultipleProperties_AreJoinedWithSemicolons()
    {
        var probe = await ProbeAsync();

        Assert.Equal("background-color:green;color:red", probe.Str("joins multiple properties with semicolons"));
    }

    [Fact(DisplayName = "handles numeric values")]
    public async Task ToStyleString_NumericValues_AreWritten()
    {
        var probe = await ProbeAsync();

        Assert.Equal("z-index:10", probe.Str("handles numeric values"));
    }

    [Fact(DisplayName = "handles zero as a value")]
    public async Task ToStyleString_Zero_IsWritten()
    {
        var probe = await ProbeAsync();

        Assert.Equal("margin:0", probe.Str("handles zero as a value"));
    }

    [Fact(DisplayName = "filters out empty string values")]
    public async Task ToStyleString_EmptyStrings_AreDropped()
    {
        var probe = await ProbeAsync();

        Assert.Equal("background:red", probe.Str("filters out empty string values"));
    }

    [Fact(DisplayName = "filters out whitespace-only string values")]
    public async Task ToStyleString_WhitespaceOnlyStrings_AreDropped()
    {
        var probe = await ProbeAsync();

        Assert.Equal("background:red", probe.Str("filters out whitespace-only string values"));
    }

    [Fact(DisplayName = "filters out null/undefined/boolean values")]
    public async Task ToStyleString_NullUndefinedAndBooleans_AreDropped()
    {
        var probe = await ProbeAsync();

        Assert.Equal("margin:auto", probe.Str("filters out null/undefined/boolean values"));
    }

    [Fact(DisplayName = "preserves quoted values")]
    public async Task ToStyleString_QuotedValues_AreKept()
    {
        var probe = await ProbeAsync();

        Assert.Equal("background-image:url(\"a\")", probe.Str("preserves quoted values"));
    }

    // #endregion

    // #region style object via addAttribute

    [Fact(DisplayName = "converts style object to attribute string")]
    public async Task StyleAttribute_Object_IsAnAttribute()
    {
        var probe = await ProbeAsync();

        Assert.Equal(" style=\"background-color:green\"", probe.Str("converts style object to attribute string"));
    }

    [Fact(DisplayName = "converts style object with multiple properties")]
    public async Task StyleAttribute_ObjectWithSeveralProperties_IsAnAttribute()
    {
        var probe = await ProbeAsync();

        Assert.Equal(
            " style=\"background-color:blue;color:white\"", probe.Str("converts style object with multiple properties"));
    }

    [Fact(DisplayName = "handles url() with quotes in style object")]
    public async Task StyleAttribute_UrlWithQuotes_IsEscaped()
    {
        var probe = await ProbeAsync();

        Assert.Equal(
            " style=\"background-image:url(&quot;a&quot;)\"", probe.Str("handles url() with quotes in style object"));
    }

    [Fact(DisplayName = "passes through string style values unchanged")]
    public async Task StyleAttribute_String_IsUnchanged()
    {
        var probe = await ProbeAsync();

        Assert.Equal(" style=\"background-color:red\"", probe.Str("passes through string style values unchanged"));
    }

    [Fact(DisplayName = "handles style array [object, string] syntax")]
    public async Task StyleAttribute_ArrayOfObjectAndString_IsJoined()
    {
        var probe = await ProbeAsync();

        Assert.Equal(" style=\"color:red;font-size:16px\"", probe.Str("handles style array [object, string] syntax"));
    }

    // #endregion

    // #region Forwarded to components

    [Fact(DisplayName = "forwards class:list values through component props")]
    public async Task ClassList_ForwardedToAComponent_BecomesTheClass()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/class-list/forwarded");

        Assert.Equal(200, response.StatusCode);
        var document = Html(response);
        foreach (var classes in ForwardedClasses)
        {
            var span = Assert.Single(document.QuerySelectorAll($"[class=\"{classes}\"]"));
            Assert.Equal(classes, span.TextContent);
        }

        var withoutClass = Assert.Single(document.QuerySelectorAll("span:not([class])"));
        Assert.Equal("", withoutClass.TextContent);
    }

    [Fact(DisplayName = "forwards style values through component props")]
    public async Task Style_ForwardedToAComponent_BecomesTheStyle()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/class-list/style-forwarded");

        Assert.Equal(200, response.StatusCode);
        var document = Html(response);
        Assert.Single(document.QuerySelectorAll("[style=\"background-color:green\"]"));
        Assert.Single(document.QuerySelectorAll("[style=\"background-color:red\"]"));
        Assert.Single(document.QuerySelectorAll("[style=\"background-color:blue\"]"));
        Assert.Single(document.QuerySelectorAll("[style='background-image:url(\"a\")']"));
    }

    // #endregion
}
