namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/cookies/error.test.ts</c>: <c>Astro.cookies.set()</c> throws a
/// <c>ResponseSentError</c> when the response was already sent.
/// <para>
/// Differences from upstream (adapted): upstream marks the request by hand (<c>request[Symbol.for('astro.responseSent')] = true</c>)
/// and calls <c>set()</c>; the <c>cookie-ops</c> endpoint of the <c>app-cookies</c> fixture does both on the <c>Astro.cookies</c>
/// and the request of a real request inside Jint (steps <c>markResponseSent</c> and <c>set</c>) and answers with the name of
/// the error. The mark is real only for a response that Astro has begun to send, which the host never does before the endpoint
/// returns (it renders without streaming, the difference behind GAP-UNITS-5), so a cookie set late in a component cannot meet it.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/cookies/error.test.ts")]
public class CookiesErrorTests(SiteHost sites)
{
    [Fact(DisplayName = "Produces an error if the response is already sent")]
    public async Task Set_ThrowsWhenTheResponseWasSent()
    {
        var run = await sites.RunCookieOpsAsync(
            null,
            new { op = "markResponseSent" },
            new { op = "set", name = "foo", value = "bar" });

        Assert.Equal("ResponseSentError", run.Error(1));
    }
}
