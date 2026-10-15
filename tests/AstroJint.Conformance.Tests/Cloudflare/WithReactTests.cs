using System.Net;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/with-react.test.ts</c>. The first test: a page that uses a React
/// component, rendered on demand, has the markup of the component. The page is <c>/react</c> of the shared fixture
/// <c>cf-ui</c>, where the upstream fixture has it at <c>/</c>; the site is one build with all the UI frameworks, so React
/// only compiles the files of <c>components/react</c>.
/// <para>
/// The second upstream test (<c>picomatch should be pre-optimized</c>) reads the log of the build for the message of the
/// Vite dependency optimizer that says it found picomatch late. The test reads <c>BuildLog</c> of the shared build for the
/// same message. Upstream clears <c>node_modules/.vite</c> before it builds; the harness keeps that cache between builds,
/// so the optimizer may have less to find here than there.
/// </para>
/// </summary>
[Trait("Upstream", "cloudflare/with-react.test.ts")]
public class WithReactTests(SiteHost sites)
{
    private const string Fixture = "cf-ui";

    [Fact(DisplayName = "renders the react component")]
    public async Task RendersTheReactComponent()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/react");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.ReadHtmlAsync();
        Assert.Equal("React Content", document.QuerySelector(".react")?.TextContent);
    }

    [Fact(DisplayName = "picomatch should be pre-optimized")]
    public async Task Picomatch_IsPreOptimized()
    {
        var site = await sites.GetAsync(Fixture);

        var late = site.BuildLog.Split('\n')
            .FirstOrDefault(line => line.Contains("new dependencies optimized") && line.Contains("picomatch"));

        Assert.True(late is null, $"Should not see \"new dependencies optimized: picomatch\" message, but got: {late}");
    }
}
