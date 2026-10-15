using static AstroJint.Conformance.CoreUnits.AppRouting;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/fetch/provider-finalize.test.ts</c>: a context provider whose <c>finalize</c> callback
/// rejects must not leave the request without a response; Astro answers 500.
/// <para>
/// Differences from upstream: upstream calls <c>getFetchStateFromAPIContext(context)</c>, an internal function, to get the state
/// of the request out of the context of an endpoint. The endpoint of the <c>app-routing</c> fixture
/// (<c>src/routes/finalize-failure.ts</c>) reads it from the same place the function does, <c>context[Symbol.for('astro.fetchState')]</c>,
/// and registers the same provider (<c>provide</c>, <c>resolve</c>, a <c>finalize</c> that rejects with 'finalize failure').
/// </para>
/// </summary>
[Trait("Upstream", "core-units/fetch/provider-finalize.test.ts")]
public class ProviderFinalizeTests(SiteHost sites)
{
    private static readonly object Probe = new
    {
        routes = new[] { Route("/probe", "finalize-failure.ts") },
    };

    [Fact(DisplayName = "still produces a response when a provider finalize callback rejects")]
    public async Task ProviderWhoseFinalizeRejects_StillGivesAResponse()
    {
        var site = await sites.GetAsync(Fixture, Probe);

        var response = await site.SendRawAsync("/probe");

        Assert.Equal(500, response.StatusCode);
    }
}
