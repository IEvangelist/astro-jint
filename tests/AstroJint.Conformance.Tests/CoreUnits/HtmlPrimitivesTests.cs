using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/render/html-primitives.test.ts</c>: the functions of Astro's runtime that write HTML
/// (<c>toAttributeString</c>, <c>toStyleString</c>, <c>defineScriptVars</c>, <c>formatList</c>, <c>renderElement</c>,
/// <c>addAttribute</c>, <c>internalSpreadAttributes</c>, <c>renderHTMLElement</c>, the <c>Fragment</c> component) and what a page
/// made with them looks like (spread attributes, void and non-void elements chosen at run time, a plain page, an empty class
/// body, forward slashes in <c>href</c>s, a <c>Fragment</c>, the order in which sibling components render).
/// <para>
/// Seven tests are ported. Upstream builds those pages with <c>createComponent()</c> and <c>renderTemplate</c>; the pages
/// <c>primitives/spread</c>, <c>primitives/input</c>, <c>primitives/basic</c> and <c>primitives/order</c> of the
/// <c>app-render</c> fixture are the same markup written as <c>.astro</c> files (the <c>Input</c> component picks
/// <c>input</c>, <c>select</c> or <c>textarea</c> from its <c>type</c> prop and renders it as a dynamic tag, as upstream's
/// <c>Input.astro</c> did; the three order components record <c>A</c> and <c>B</c> in <c>Astro.locals</c> where upstream used
/// a global), and the assertions are upstream's. The other 48 tests call the runtime functions directly and have no route; they
/// are adapted: the probe endpoint of the fixture (<c>src/pages/probe/[name].ts</c>, module <c>html-primitives</c>) makes the
/// same calls with the same arguments inside the host, with the stub of <c>customElements</c> that the
/// <c>renderHTMLElement</c> tests set and restore, and answers with what they returned; the tests assert upstream's expected
/// values on that answer, so they prove that these functions behave the same inside Jint. The test that starts a
/// <c>Fragment</c> render without awaiting it ('streams sync siblings before async children resolve (issue #13283)') runs
/// inside the probe in full, with the same microtask turn between starting the render and looking at the chunks, and the four
/// assertions are made on its result. The two tests titled 'drops malicious keys while keeping valid ones' (one for
/// <c>spreadAttributes</c>, one for <c>renderHTMLElement</c>) carry the title of their <c>describe</c> in front of it.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/render/html-primitives.test.ts")]
public class HtmlPrimitivesTests(SiteHost sites)
{
    // The tags that the XSS tests build their values from, which must not come out as they are.
    private static readonly string[] CaseVariants = ["</Script>", "</SCRIPT>", "</sCrIpT>"];
    private static readonly string[] WhitespaceVariants = ["</script >", "</script\t>", "</script\n>"];

    private async Task<Probe> ProbeAsync()
    {
        var site = await sites.GetAsync(Fixture);
        return await site.ProbeAsync("html-primitives");
    }

    private async Task<RawResponse> GetPageAsync(string path)
    {
        var site = await sites.GetAsync(Fixture);
        var response = await site.SendRawAsync(path);
        Assert.Equal(200, response.StatusCode);
        return response;
    }

    // #region toAttributeString

    [Fact(DisplayName = "escapes & to &amp;")]
    public async Task ToAttributeString_Ampersand_IsEscaped()
    {
        var probe = await ProbeAsync();

        Assert.Equal("a&amp;b", probe.Str("escapes & to &amp;"));
    }

    [Fact(DisplayName = "escapes \" to &quot;")]
    public async Task ToAttributeString_DoubleQuote_IsEscaped()
    {
        var probe = await ProbeAsync();

        Assert.Equal("say &quot;hello&quot;", probe.Str("escapes \" to &quot;"));
    }

    [Fact(DisplayName = "escapes both & and \" in the same string")]
    public async Task ToAttributeString_AmpersandAndDoubleQuote_AreEscaped()
    {
        var probe = await ProbeAsync();

        Assert.Equal("&quot;a&amp;b&quot;", probe.Str("escapes both & and \" in the same string"));
    }

    [Fact(DisplayName = "passes through normal strings unchanged")]
    public async Task ToAttributeString_NormalString_IsUnchanged()
    {
        var probe = await ProbeAsync();

        Assert.Equal("hello world", probe.Str("passes through normal strings unchanged"));
    }

    [Fact(DisplayName = "does not escape when shouldEscape is false")]
    public async Task ToAttributeString_ShouldEscapeFalse_IsUnchanged()
    {
        var probe = await ProbeAsync();

        Assert.Equal("a&b", probe.Str("does not escape when shouldEscape is false"));
    }

    [Fact(DisplayName = "coerces non-string values to string")]
    public async Task ToAttributeString_NonStringValues_AreStrings()
    {
        var probe = await ProbeAsync();

        probe.AssertStrings("coerces non-string values to string", "42", "true");
    }

    // #endregion

    // #region toStyleString

    [Fact(DisplayName = "converts camelCase properties to kebab-case")]
    public async Task ToStyleString_CamelCaseProperties_BecomeKebabCase()
    {
        var probe = await ProbeAsync();

        Assert.Equal("background-color:red", probe.Str("converts camelCase properties to kebab-case"));
    }

    [Fact(DisplayName = "leaves lowercase properties unchanged")]
    public async Task ToStyleString_LowercaseProperties_AreUnchanged()
    {
        var probe = await ProbeAsync();

        Assert.Equal("color:blue", probe.Str("leaves lowercase properties unchanged"));
    }

    [Fact(DisplayName = "leaves CSS custom properties (--) unchanged")]
    public async Task ToStyleString_CustomProperties_AreUnchanged()
    {
        var probe = await ProbeAsync();

        Assert.Equal("--my-var:10px", probe.Str("leaves CSS custom properties (--) unchanged"));
    }

    [Fact(DisplayName = "joins multiple properties with semicolons")]
    public async Task ToStyleString_MultipleProperties_AreJoinedWithSemicolons()
    {
        var probe = await ProbeAsync();

        Assert.Equal("color:red;font-size:16px", probe.Str("joins multiple properties with semicolons"));
    }

    [Fact(DisplayName = "includes numeric values")]
    public async Task ToStyleString_NumericValues_AreIncluded()
    {
        var probe = await ProbeAsync();

        probe.AssertStrings("includes numeric values", "opacity:0", "z-index:99");
    }

    [Fact(DisplayName = "filters out empty string values")]
    public async Task ToStyleString_EmptyStrings_AreFilteredOut()
    {
        var probe = await ProbeAsync();

        Assert.Equal("", probe.Str("filters out empty string values"));
    }

    [Fact(DisplayName = "filters out null and undefined values")]
    public async Task ToStyleString_NullAndUndefined_AreFilteredOut()
    {
        var probe = await ProbeAsync();

        Assert.Equal("", probe.Str("filters out null and undefined values"));
    }

    [Fact(DisplayName = "returns empty string for empty object")]
    public async Task ToStyleString_EmptyObject_IsEmpty()
    {
        var probe = await ProbeAsync();

        Assert.Equal("", probe.Str("returns empty string for empty object"));
    }

    // #endregion

    // #region defineScriptVars

    [Fact(DisplayName = "generates const declaration for a string value")]
    public async Task DefineScriptVars_String_IsAConst()
    {
        var probe = await ProbeAsync();

        Assert.Contains("const name = \"Astro\";", probe.Str("generates const declaration for a string value"));
    }

    [Fact(DisplayName = "generates const declaration for a number value")]
    public async Task DefineScriptVars_Number_IsAConst()
    {
        var probe = await ProbeAsync();

        Assert.Contains("const count = 42;", probe.Str("generates const declaration for a number value"));
    }

    [Fact(DisplayName = "generates const declaration for an object value")]
    public async Task DefineScriptVars_Object_IsAConst()
    {
        var probe = await ProbeAsync();

        Assert.Contains("const config = {\"debug\":true};", probe.Str("generates const declaration for an object value"));
    }

    [Fact(DisplayName = "generates multiple const declarations")]
    public async Task DefineScriptVars_SeveralValues_AreConsts()
    {
        var probe = await ProbeAsync();

        var result = probe.Str("generates multiple const declarations");

        Assert.Contains("const a = 1;", result);
        Assert.Contains("const b = 2;", result);
    }

    [Fact(DisplayName = "sanitizes </script> to prevent XSS injection")]
    public async Task DefineScriptVars_ClosingScriptTag_IsSanitized()
    {
        var probe = await ProbeAsync();

        var result = probe.Str("sanitizes </script> to prevent XSS injection");

        Assert.DoesNotContain("</script>", result);
        Assert.Contains("\\u003c/script>", result);
    }

    [Fact(DisplayName = "sanitizes case-insensitive </script> variants")]
    public async Task DefineScriptVars_ClosingScriptTagInAnyCase_IsSanitized()
    {
        var probe = await ProbeAsync();

        var results = probe.Strings("sanitizes case-insensitive </script> variants");

        Assert.Equal(CaseVariants.Length, results.Length);
        for (var i = 0; i < CaseVariants.Length; i++)
        {
            Assert.DoesNotContain(CaseVariants[i], results[i]);
        }
    }

    [Fact(DisplayName = "sanitizes </script> with trailing whitespace before >")]
    public async Task DefineScriptVars_ClosingScriptTagWithWhitespace_IsSanitized()
    {
        var probe = await ProbeAsync();

        var results = probe.Strings("sanitizes </script> with trailing whitespace before >");

        Assert.Equal(WhitespaceVariants.Length, results.Length);
        for (var i = 0; i < WhitespaceVariants.Length; i++)
        {
            Assert.DoesNotContain(WhitespaceVariants[i], results[i]);
        }
    }

    [Fact(DisplayName = "sanitizes self-closing </script/>")]
    public async Task DefineScriptVars_SelfClosingScriptTag_IsSanitized()
    {
        var probe = await ProbeAsync();

        Assert.DoesNotContain("</script/>", probe.Str("sanitizes self-closing </script/>"));
    }

    [Fact(DisplayName = "handles undefined values without throwing")]
    public async Task DefineScriptVars_Undefined_IsAConst()
    {
        var probe = await ProbeAsync();

        Assert.Contains("const undef = undefined;", probe.Str("handles undefined values without throwing"));
    }

    [Fact(DisplayName = "converts keys with spaces to valid JS identifiers")]
    public async Task DefineScriptVars_KeysWithSpaces_BecomeIdentifiers()
    {
        var probe = await ProbeAsync();

        Assert.Contains("const myKey = \"value\";", probe.Str("converts keys with spaces to valid JS identifiers"));
    }

    // #endregion

    // #region formatList

    [Fact(DisplayName = "returns the single item for a one-element array")]
    public async Task FormatList_OneItem_IsTheItem()
    {
        var probe = await ProbeAsync();

        Assert.Equal("only", probe.Str("returns the single item for a one-element array"));
    }

    [Fact(DisplayName = "joins two items with \"or\"")]
    public async Task FormatList_TwoItems_AreJoinedWithOr()
    {
        var probe = await ProbeAsync();

        Assert.Equal("a or b", probe.Str("joins two items with \"or\""));
    }

    [Fact(DisplayName = "joins three items with comma and \"or\"")]
    public async Task FormatList_ThreeItems_AreJoinedWithCommaAndOr()
    {
        var probe = await ProbeAsync();

        Assert.Equal("a, b or c", probe.Str("joins three items with comma and \"or\""));
    }

    [Fact(DisplayName = "joins four or more items correctly")]
    public async Task FormatList_FourItems_AreJoinedWithCommasAndOr()
    {
        var probe = await ProbeAsync();

        Assert.Equal("a, b, c or d", probe.Str("joins four or more items correctly"));
    }

    // #endregion

    // #region renderElement

    [Fact(DisplayName = "renders a void element without a closing tag")]
    public async Task RenderElement_VoidElement_HasNoClosingTag()
    {
        var probe = await ProbeAsync();

        Assert.Equal("<br>", probe.Str("renders a void element without a closing tag"));
    }

    [Fact(DisplayName = "renders a void element with attributes")]
    public async Task RenderElement_VoidElementWithAttributes_HasThem()
    {
        var probe = await ProbeAsync();

        Assert.Equal("<img src=\"/hero.png\" alt=\"Hero\">", probe.Str("renders a void element with attributes"));
    }

    [Fact(DisplayName = "renders a non-void element with children")]
    public async Task RenderElement_NonVoidElement_HasItsChildren()
    {
        var probe = await ProbeAsync();

        Assert.Equal("<div>Hello</div>", probe.Str("renders a non-void element with children"));
    }

    [Fact(DisplayName = "renders a non-void element with attributes and children")]
    public async Task RenderElement_NonVoidElementWithAttributes_HasThemAndItsChildren()
    {
        var probe = await ProbeAsync();

        Assert.Equal("<span class=\"bold\">text</span>", probe.Str("renders a non-void element with attributes and children"));
    }

    [Fact(DisplayName = "renders a script element with children")]
    public async Task RenderElement_ScriptElement_HasItsChildren()
    {
        var probe = await ProbeAsync();

        Assert.Equal("<script>console.log(1)</script>", probe.Str("renders a script element with children"));
    }

    [Fact(DisplayName = "does not render lang, data-astro-id props")]
    public async Task RenderElement_LangAndDataAstroId_AreNotRendered()
    {
        var probe = await ProbeAsync();

        var result = probe.Str("does not render lang, data-astro-id props");

        Assert.DoesNotContain("lang=", result);
        Assert.DoesNotContain("data-astro-id=", result);
    }

    [Fact(DisplayName = "injects defineVars into script children")]
    public async Task RenderElement_DefineVars_AreInjectedIntoScripts()
    {
        var probe = await ProbeAsync();

        var result = probe.Str("injects defineVars into script children");

        Assert.Contains("const count = 5;", result);
        Assert.Contains("console.log(count)", result);
    }

    // #endregion

    // #region addAttribute rejects invalid attribute keys

    [Fact(DisplayName = "drops keys containing double quotes")]
    public async Task AddAttribute_KeyWithDoubleQuotes_IsDropped()
    {
        var probe = await ProbeAsync();

        Assert.Equal("", probe.Str("drops keys containing double quotes"));
    }

    [Fact(DisplayName = "drops keys containing spaces")]
    public async Task AddAttribute_KeyWithSpaces_IsDropped()
    {
        var probe = await ProbeAsync();

        Assert.Equal("", probe.Str("drops keys containing spaces"));
    }

    [Fact(DisplayName = "drops keys containing >")]
    public async Task AddAttribute_KeyWithGreaterThan_IsDropped()
    {
        var probe = await ProbeAsync();

        Assert.Equal("", probe.Str("drops keys containing >"));
    }

    [Fact(DisplayName = "drops keys containing single quotes")]
    public async Task AddAttribute_KeyWithSingleQuotes_IsDropped()
    {
        var probe = await ProbeAsync();

        Assert.Equal("", probe.Str("drops keys containing single quotes"));
    }

    [Fact(DisplayName = "drops keys containing =")]
    public async Task AddAttribute_KeyWithEquals_IsDropped()
    {
        var probe = await ProbeAsync();

        Assert.Equal("", probe.Str("drops keys containing ="));
    }

    [Fact(DisplayName = "allows normal attribute names")]
    public async Task AddAttribute_NormalNames_AreWritten()
    {
        var probe = await ProbeAsync();

        probe.AssertStrings("allows normal attribute names", " id=\"v\"", " data-foo=\"v\"", " aria-label=\"v\"");
    }

    [Fact(DisplayName = "allows namespaced attributes")]
    public async Task AddAttribute_NamespacedNames_AreWritten()
    {
        var probe = await ProbeAsync();

        probe.AssertStrings("allows namespaced attributes", " on:click=\"v\"", " xmlns:happy=\"v\"");
    }

    // #endregion

    // #region spreadAttributes rejects invalid attribute keys

    [Fact(DisplayName = "spreadAttributes rejects invalid attribute keys: drops malicious keys while keeping valid ones")]
    public async Task SpreadAttributes_MaliciousKeys_AreDropped()
    {
        var probe = await ProbeAsync();

        var result = probe.Str("spreadAttributes rejects invalid attribute keys: drops malicious keys while keeping valid ones");

        Assert.Contains("class=\"safe\"", result);
        Assert.Contains("id=\"ok\"", result);
        Assert.DoesNotContain("onclick", result);
        Assert.DoesNotContain("alert", result);
    }

    // #endregion

    // #region renderHTMLElement rejects invalid attribute keys

    [Fact(DisplayName = "renderHTMLElement rejects invalid attribute keys: drops malicious keys while keeping valid ones")]
    public async Task RenderHtmlElement_MaliciousKeys_AreDropped()
    {
        var probe = await ProbeAsync();

        var output = probe.Str("renderHTMLElement rejects invalid attribute keys: drops malicious keys while keeping valid ones");

        Assert.Contains("data-safe=\"ok\"", output);
        Assert.DoesNotContain("onmouseover", output);
        Assert.DoesNotContain("<script>", output);
        Assert.DoesNotContain("alert", output);
    }

    [Fact(DisplayName = "preserves namespaced and normal attribute names")]
    public async Task RenderHtmlElement_NormalAndNamespacedNames_AreKept()
    {
        var probe = await ProbeAsync();

        var output = probe.Str("preserves namespaced and normal attribute names");

        Assert.Contains("id=\"a\"", output);
        Assert.Contains("data-foo=\"b\"", output);
        Assert.Contains("on:click=\"c\"", output);
    }

    // #endregion

    // #region Correctly serializes boolean attributes (#astro-basic)

    [Fact(DisplayName = "renders empty-value attribute for data-* attr with no value")]
    public async Task AddAttribute_EmptyDataAttribute_HasNoValue()
    {
        var probe = await ProbeAsync();

        Assert.Equal(" data-something", probe.Str("renders empty-value attribute for data-* attr with no value"));
    }

    [Fact(DisplayName = "renders empty-value attribute for arbitrary attr with no value")]
    public async Task AddAttribute_EmptyArbitraryAttribute_HasNoValue()
    {
        var probe = await ProbeAsync();

        Assert.Equal(" not-data-ok", probe.Str("renders empty-value attribute for arbitrary attr with no value"));
    }

    // #endregion

    // #region Allows spread attributes (#521)

    [Fact(DisplayName = "spreads attributes correctly regardless of position")]
    public async Task SpreadAttributes_BeforeAndAfterOtherAttributes_AreRendered()
    {
        var response = await GetPageAsync("/primitives/spread");

        var document = Html(response);
        var leading = document.QuerySelector("#spread-leading")!;
        var trailing = document.QuerySelector("#spread-trailing")!;
        Assert.Equal("0", leading.GetAttribute("a"));
        Assert.Equal("1", leading.GetAttribute("b"));
        Assert.Equal("2", leading.GetAttribute("c"));
        Assert.Equal("0", trailing.GetAttribute("a"));
        Assert.Equal("1", trailing.GetAttribute("b"));
        Assert.Equal("2", trailing.GetAttribute("c"));
    }

    // #endregion

    // #region Supports void elements whose name is a string (#2062)

    [Fact(DisplayName = "renders void/non-void elements correctly based on runtime tag name")]
    public async Task DynamicTag_ChosenAtRunTime_IsVoidOrNot()
    {
        var response = await GetPageAsync("/primitives/input");

        var body = Html(response).Body!;
        Assert.Equal(3, body.Children.Length);
        Assert.Equal("<input>", body.Children[0].OuterHtml);
        Assert.Equal("<input type=\"password\">", body.Children[1].OuterHtml);
        Assert.Equal("<input type=\"text\">", body.Children[2].OuterHtml);
    }

    // #endregion

    // #region Can load page, Selector with an empty body, Allows forward-slashes in mustache tags (#407)

    [Fact(DisplayName = "renders h1 content")]
    public async Task Page_RendersItsHeading()
    {
        var response = await GetPageAsync("/primitives/basic");

        Assert.Equal("Hello world!", AppRouting.Text(response, "h1"));
    }

    [Fact(DisplayName = "renders element with empty CSS class body")]
    public async Task Element_WithAnEmptyClassBody_IsRendered()
    {
        var response = await GetPageAsync("/primitives/basic");

        Assert.Single(Html(response).QuerySelectorAll(".author"));
    }

    [Fact(DisplayName = "renders hrefs with forward slashes from template expressions")]
    public async Task Hrefs_WithForwardSlashes_AreRendered()
    {
        var response = await GetPageAsync("/primitives/basic");

        var document = Html(response);
        Assert.Single(document.QuerySelectorAll("a[href=\"/post/one\"]"));
        Assert.Single(document.QuerySelectorAll("a[href=\"/post/two\"]"));
        Assert.Single(document.QuerySelectorAll("a[href=\"/post/three\"]"));
    }

    // #endregion

    // #region Allows using the Fragment element

    [Fact(DisplayName = "renders Fragment children without a wrapper element")]
    public async Task Fragment_ChildrenAreRenderedWithoutAWrapper()
    {
        var response = await GetPageAsync("/primitives/basic");

        var children = Html(response).QuerySelector("ul")!.Children;
        var item = Assert.Single(children);
        Assert.Equal("one", item.Id);
        Assert.Equal("li", item.LocalName);
    }

    [Fact(DisplayName = "streams sync siblings before async children resolve (issue #13283)")]
    public async Task Fragment_SyncSiblingsAreWrittenBeforeTheAsyncChildResolves()
    {
        var probe = await ProbeAsync();

        var result = probe["streams sync siblings before async children resolve (issue #13283)"];

        Assert.True(result.GetProperty("syncFlushed").GetBoolean(), "sync sibling should stream before async child resolves");
        Assert.True(result.GetProperty("hasSync").GetBoolean(), "sync content present in final output");
        Assert.True(result.GetProperty("hasAsync").GetBoolean(), "async content present in final output");
        Assert.True(result.GetProperty("syncBeforeAsync").GetBoolean(), "sync appears before async in output");
    }

    // #endregion

    // #region renders the components top-down

    [Fact(DisplayName = "renders sibling components in document order")]
    public async Task SiblingComponents_RenderInDocumentOrder()
    {
        var response = await GetPageAsync("/primitives/order");

        Assert.Equal("Rendered order: A, B", AppRouting.Text(response, "#rendered-order"));
    }

    // #endregion
}
