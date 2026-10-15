using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/render/streaming.test.ts</c>: a page with ten async components answers with its whole
/// body, a sync sibling inside a <c>Fragment</c> comes before the async one, a page whose render fails answers 500 with no body,
/// and a render with streaming off has a content type and a length.
/// <para>
/// Differences from upstream: 'body is chunked with async components' is not ported (N/A): the host buffers the body of every
/// response before it writes it (Jint's fetch hosting cannot read a stream-backed body, so the server entry always
/// answers with the whole body), so there is never more than one chunk to count; the tests that read the chunks read the whole
/// body, as <c>response.text()</c> does. 'sync sibling inside Fragment streams before async child resolves' is adapted: it
/// checks the order of the two paragraphs in the body instead of the order of the chunks. 'stays alive on failed component
/// renders' requests <c>multiple-errors.astro</c> of the <c>app-render</c> fixture, whose frontmatter throws a
/// <c>TypeError</c>; the fixture's 500 page fails for any error but its own, so the response is the bare 500. The page of the
/// 'Streaming disabled' test is the same as the others (the host always runs the app with streaming off).
/// </para>
/// </summary>
[Trait("Upstream", "core-units/render/streaming.test.ts")]
public class StreamingTests(SiteHost sites)
{
    [Fact(DisplayName = "can get the full html body")]
    public async Task Page_HasItsFullBody()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/streaming/page");

        Assert.Contains("<header>", response.Text);
        Assert.Contains("<h1>My Site</h1>", response.Text);
        Assert.Equal(10, response.Text.Split("<li>").Length - 1);
    }

    [Fact(DisplayName = "response body is correctly served from cache of chunks")]
    public async Task Page_HasTheFirstAndTheLastItem()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/streaming/page");

        Assert.Contains("Item 1", response.Text);
        Assert.Contains("Item 10", response.Text);
    }

    [Fact(DisplayName = "sync sibling inside Fragment streams before async child resolves")]
    public async Task SyncSiblingInAFragment_ComesBeforeTheAsyncChild()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/streaming/fragment");

        var sync = response.Text.IndexOf("sync-in-fragment", StringComparison.Ordinal);
        var async = response.Text.IndexOf("async-in-fragment", StringComparison.Ordinal);
        Assert.NotEqual(-1, sync);
        Assert.NotEqual(-1, async);
        Assert.True(sync < async);
    }

    [Fact(DisplayName = "stays alive on failed component renders")]
    public async Task FailedRender_Answers500WithoutABody()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/streaming/multiple-errors");

        Assert.Equal(500, response.StatusCode);
        Assert.Equal("", response.Text);
    }

    [Fact(DisplayName = "can get the full html body with content-length")]
    public async Task StreamingDisabled_HasTheBodyAndItsLength()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/streaming/page");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("text/html", response.Header("content-type"));
        Assert.True(int.Parse(response.Header("content-length")!) > 0);
        Assert.Contains("<header>", response.Text);
        Assert.Equal(10, response.Text.Split("<li>").Length - 1);
    }
}
