using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/app/astro-attrs.test.ts</c>: the attributes that Astro's <c>addAttribute()</c> writes
/// for the values of expressions (booleans, empty strings, numbers, <c>null</c>/<c>undefined</c>, escaped characters, the
/// <c>download</c>, <c>popover</c> and <c>hidden</c> attributes that take a boolean or a string), the namespaced attributes
/// of an element, and a namespaced attribute that a component receives as a prop and spreads.
/// <para>
/// The three tests are ported with real pages: <c>attributes</c>, <c>attributes/namespaced</c> and
/// <c>attributes/namespaced-component</c> of the <c>app-render</c> fixture are the three inline pages of upstream, and the
/// compiler turns their expression attributes into the same <c>addAttribute()</c> calls (the values are constants of the
/// frontmatter, because a literal could be folded into the markup). What differs: the HTML is parsed by AngleSharp where upstream
/// uses cheerio, which reports a boolean attribute such as <c>hidden</c> by its name (<c>'hidden'</c>) and AngleSharp by its value
/// (<c>""</c>), so the tests expect <c>""</c> for an attribute that is present without a value (the raw checks on the
/// markup are upstream's); the command in the <c>code</c> attribute redirects to <c>out.txt</c>; and the pattern of upstream's fourth
/// raw check looks for <c>data-foobar=</c> after the id of a normal attribute, which cannot match, so the test looks for
/// <c>foobar=</c>, the attribute that its message talks about.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/app/astro-attrs.test.ts")]
public class AstroAttrsTests(SiteHost sites)
{
    // The element, the attribute and the value that the page gives it (null when the attribute is absent).
    private static readonly (string Id, string Attribute, string? Value)[] Expected =
    [
        ("download-true", "download", ""),
        ("download-false", "download", null),
        ("download-undefined", "download", null),
        ("download-string-empty", "download", ""),
        ("download-string", "download", "my-document.pdf"),
        ("popover-auto", "popover", "auto"),
        ("popover-true", "popover", ""),
        ("popover-false", "popover", null),
        ("popover-string-empty", "popover", ""),
        ("popover-custom-el-true", "popover", ""),
        ("popover-custom-el-false", "popover", null),
        ("popover-custom-el-auto", "popover", "auto"),
        ("hidden-true", "hidden", ""),
        ("hidden-false", "hidden", null),
        ("hidden-string-empty", "hidden", ""),
        ("boolean-attr-true", "allowfullscreen", ""),
        ("boolean-attr-false", "allowfullscreen", null),
        ("boolean-attr-string-truthy", "allowfullscreen", ""),
        ("boolean-attr-string-falsy", "allowfullscreen", null),
        ("boolean-attr-number-truthy", "allowfullscreen", ""),
        ("boolean-attr-number-falsy", "allowfullscreen", null),
        ("data-attr-true", "data-foobar", "true"),
        ("data-attr-false", "data-foobar", "false"),
        ("data-attr-string-truthy", "data-foobar", "foo"),
        ("data-attr-string-falsy", "data-foobar", ""),
        ("data-attr-number-truthy", "data-foobar", "1"),
        ("data-attr-number-falsy", "data-foobar", "0"),
        ("normal-attr-true", "foobar", "true"),
        ("normal-attr-false", "foobar", "false"),
        ("normal-attr-string-truthy", "foobar", "foo"),
        ("normal-attr-string-falsy", "foobar", ""),
        ("normal-attr-number-truthy", "foobar", "1"),
        ("normal-attr-number-falsy", "foobar", "0"),
        ("null", "attr", null),
        ("undefined", "attr", null),
        ("html-enum", "draggable", "true"),
        ("html-enum-true", "draggable", "true"),
        ("html-enum-false", "draggable", "false"),
    ];

    [Fact(DisplayName = "Passes attributes to elements as expected")]
    public async Task Attributes_AreRenderedAsExpected()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/attributes");

        Assert.Equal(200, response.StatusCode);
        var html = response.Text;
        var document = Html(response);

        // A parser would normalize hidden="until-found" and drop the escaping, so these checks read the markup.
        Assert.Contains("id=\"hidden-until-found\" hidden=\"until-found\"", html);
        Assert.DoesNotContain("allowfullscreen=", html);
        Assert.DoesNotMatch(@"id=""data-attr-string-falsy""\s+data-foobar=", html);
        Assert.DoesNotMatch(@"id=""normal-attr-string-falsy""\s+foobar=", html);
        Assert.Contains("https://example.com/api/og?title=hello&amp;description=somedescription", html);
        Assert.Contains("cmd: echo &quot;foo&quot; &amp;&amp; echo &quot;bar&quot; > out.txt", html);

        List<string> mismatches = [];
        foreach (var (id, attribute, value) in Expected)
        {
            var element = document.QuerySelector($"#{id}");
            if (element is null)
            {
                mismatches.Add($"#{id}: there is no such element");
                continue;
            }

            var actual = element.GetAttribute(attribute);
            if (actual != value)
            {
                mismatches.Add($"#{id}: expected {attribute} to be {Show(value)} but it is {Show(actual)}");
            }
        }

        Assert.Empty(mismatches);
    }

    [Fact(DisplayName = "Passes namespaced attributes as expected")]
    public async Task NamespacedAttributes_AreRenderedAsExpected()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/attributes/namespaced");

        Assert.Equal(200, response.StatusCode);
        var document = Html(response);
        Assert.Equal("https://example.com/schemas/happy", document.QuerySelector("div")?.GetAttribute("xmlns:happy"));
        Assert.Equal("sweet", document.QuerySelector("img")?.GetAttribute("happy:smile"));
    }

    [Fact(DisplayName = "Passes namespaced attributes to components as expected")]
    public async Task NamespacedAttributes_PassedToAComponent_AreSpread()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/attributes/namespaced-component");

        Assert.Equal(200, response.StatusCode);
        var document = Html(response);
        Assert.Equal("(event) => console.log(event)", document.QuerySelector("span")?.GetAttribute("on:click"));
    }

    private static string Show(string? value) => value is null ? "absent" : $"\"{value}\"";
}
