using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/render/escape.test.ts</c>: <c>markHTMLString()</c>, <c>isHTMLString()</c>,
/// <c>escapeHTML()</c>, the escaping of raw text (<c>escapeStyleText()</c>, <c>stringifyForScript()</c>) and <c>unescapeHTML()</c>
/// with every kind of value that <c>set:html</c> takes (string, promise, iterator, async iterator, <c>Response</c>,
/// <c>ReadableStream</c>).
/// <para>
/// All 20 tests are adapted: they call functions of <c>astro/runtime/server/escape.js</c> and have no route. The probe endpoint of
/// the <c>app-render</c> fixture (<c>src/pages/probe/[name].ts</c>, module <c>escape</c>) makes the same calls with the same
/// arguments inside the host (the <c>Response</c> and the <c>ReadableStream</c> are the ones of Jint's Web APIs) and the tests
/// assert upstream's expected values on its answer. The <c>set:html</c> path of a real page, with a <c>Response</c> too, is
/// ported in <see cref="DirectivesTests"/>.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/render/escape.test.ts")]
public class EscapeTests(SiteHost sites)
{
    private async Task<Probe> ProbeAsync()
    {
        var site = await sites.GetAsync(Fixture);
        return await site.ProbeAsync("escape");
    }

    // #region markHTMLString

    [Fact(DisplayName = "wraps a string in an HTMLString instance")]
    public async Task MarkHtmlString_WrapsAString()
    {
        var probe = await ProbeAsync();

        var value = probe["wraps a string in an HTMLString instance"];
        Assert.True(value.Bool("isHTMLString"));
        Assert.Equal("hello", value.Str("text"));
    }

    [Fact(DisplayName = "returns the value unchanged if already an HTMLString")]
    public async Task MarkHtmlString_AnHtmlString_IsUnchanged()
    {
        var probe = await ProbeAsync();

        var value = probe["returns the value unchanged if already an HTMLString"];
        Assert.True(value.Bool("isHTMLString"));
        Assert.Equal("hello", value.Str("text"));
    }

    // #endregion

    // #region isHTMLString

    [Fact(DisplayName = "returns true for HTMLString instances")]
    public async Task IsHtmlString_AnHtmlString_IsTrue()
    {
        var probe = await ProbeAsync();

        Assert.True(probe.Bool("returns true for HTMLString instances"));
    }

    [Fact(DisplayName = "returns false for plain strings")]
    public async Task IsHtmlString_APlainString_IsFalse()
    {
        var probe = await ProbeAsync();

        Assert.False(probe.Bool("returns false for plain strings"));
    }

    [Fact(DisplayName = "returns false for null and undefined")]
    public async Task IsHtmlString_NullAndUndefined_AreFalse()
    {
        var probe = await ProbeAsync();

        Assert.All(probe["returns false for null and undefined"].EnumerateArray(), item => Assert.False(item.GetBoolean()));
        Assert.Equal(2, probe["returns false for null and undefined"].GetArrayLength());
    }

    [Fact(DisplayName = "returns false for numbers and objects")]
    public async Task IsHtmlString_NumbersAndObjects_AreFalse()
    {
        var probe = await ProbeAsync();

        Assert.All(probe["returns false for numbers and objects"].EnumerateArray(), item => Assert.False(item.GetBoolean()));
        Assert.Equal(2, probe["returns false for numbers and objects"].GetArrayLength());
    }

    // #endregion

    // #region escapeHTML

    [Fact(DisplayName = "escapes < and > to &lt; and &gt;")]
    public async Task EscapeHtml_AngleBrackets_AreEscaped()
    {
        var probe = await ProbeAsync();

        Assert.Equal("&lt;script&gt;alert(&quot;xss&quot;)&lt;/script&gt;", probe.Str("escapes < and > to &lt; and &gt;"));
    }

    [Fact(DisplayName = "escapes & to &amp;")]
    public async Task EscapeHtml_Ampersand_IsEscaped()
    {
        var probe = await ProbeAsync();

        Assert.Equal("a &amp; b", probe.Str("escapes & to &amp;"));
    }

    [Fact(DisplayName = "escapes \" to &quot;")]
    public async Task EscapeHtml_DoubleQuote_IsEscaped()
    {
        var probe = await ProbeAsync();

        Assert.Equal("&quot;hello&quot;", probe.Str("escapes \" to &quot;"));
    }

    [Fact(DisplayName = "escapes ' to &#39;")]
    public async Task EscapeHtml_SingleQuote_IsEscaped()
    {
        var probe = await ProbeAsync();

        Assert.Equal("it&#39;s", probe.Str("escapes ' to &#39;"));
    }

    [Fact(DisplayName = "escapes a plain string and returns a string")]
    public async Task EscapeHtml_APlainString_ReturnsAString()
    {
        var probe = await ProbeAsync();

        var value = probe["escapes a plain string and returns a string"];
        Assert.Equal("string", value.Str("type"));
        Assert.Equal("&lt;b&gt;test&lt;/b&gt;", value.Str("value"));
    }

    // #endregion

    // #region raw text escaping

    [Fact(DisplayName = "escapes less-than signs in inline style text as CSS")]
    public async Task EscapeStyleText_LessThanSigns_AreEscapedAsCss()
    {
        var probe = await ProbeAsync();

        Assert.Equal(
            "animation-name: \\3C /STYLE>\\3C script>test\\3C /script>",
            probe.Str("escapes less-than signs in inline style text as CSS"));
    }

    [Fact(DisplayName = "preserves ordinary inline style text")]
    public async Task EscapeStyleText_OrdinaryText_IsPreserved()
    {
        var probe = await ProbeAsync();

        Assert.Equal("animation-duration: 300ms", probe.Str("preserves ordinary inline style text"));
    }

    [Fact(DisplayName = "serializes values without literal less-than signs for inline scripts")]
    public async Task StringifyForScript_LessThanSigns_AreEscaped()
    {
        var probe = await ProbeAsync();

        Assert.Equal(
            "{\"value\":\"\\u003c/SCRIPT>\\u003cscript>test\\u003c/script>\"}",
            probe.Str("serializes values without literal less-than signs for inline scripts"));
    }

    // #endregion

    // #region unescapeHTML

    [Fact(DisplayName = "can take a string of HTML \u2014 Migrated from test/set-html.test.js")]
    public async Task UnescapeHtml_AString_IsAnHtmlString()
    {
        var probe = await ProbeAsync();

        var value = probe["can take a string of HTML \u2014 Migrated from test/set-html.test.js"];
        Assert.True(value.Bool("isHTMLString"));
        Assert.Equal("<b>bold</b>", value.Str("text"));
    }

    [Fact(DisplayName = "can take a Promise to a string of HTML \u2014 Migrated from test/set-html.test.js")]
    public async Task UnescapeHtml_APromise_IsAnHtmlString()
    {
        var probe = await ProbeAsync();

        var value = probe["can take a Promise to a string of HTML \u2014 Migrated from test/set-html.test.js"];
        Assert.True(value.Bool("isHTMLString"));
        Assert.Contains("<b>bold</b>", value.Str("text"), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "can take an Iterator \u2014 Migrated from test/set-html.test.js")]
    public async Task UnescapeHtml_AnIterator_YieldsItsChunks()
    {
        var probe = await ProbeAsync();

        var text = probe.Str("can take an Iterator \u2014 Migrated from test/set-html.test.js");
        Assert.Contains("<li>1</li>", text, StringComparison.Ordinal);
        Assert.Contains("<li>2</li>", text, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "can take an AsyncIterator")]
    public async Task UnescapeHtml_AnAsyncIterator_YieldsItsChunks()
    {
        var probe = await ProbeAsync();

        Assert.Contains("<li>a</li>", probe.Str("can take an AsyncIterator"), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "can take a Response")]
    public async Task UnescapeHtml_AResponse_YieldsItsBody()
    {
        var probe = await ProbeAsync();

        Assert.Contains("<p>hello</p>", probe.Str("can take a Response"), StringComparison.Ordinal);
    }

    [Fact(DisplayName = "can take a ReadableStream")]
    public async Task UnescapeHtml_AReadableStream_YieldsItsChunks()
    {
        var probe = await ProbeAsync();

        Assert.Contains("<span>stream</span>", probe.Str("can take a ReadableStream"), StringComparison.Ordinal);
    }

    // #endregion
}
