using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/render/not-response.test.ts</c>: a page that returns a value that is not a
/// <c>Response</c> (<c>null</c>) answers 500 (<c>OnlyResponseCanBeReturned</c>).
/// <para>
/// Differences from upstream: the page is <c>not-response.astro</c> of the <c>app-render</c> fixture, which returns <c>null</c>
/// from its frontmatter; the upstream component factory returned it directly.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/render/not-response.test.ts")]
public class NotResponseTests(SiteHost sites)
{
    [Fact(DisplayName = "produces a 500 when a page returns a non-Response value")]
    public async Task PageThatReturnsNull_Answers500()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/not-response");

        Assert.Equal(500, response.StatusCode);
    }
}
