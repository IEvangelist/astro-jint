using System.Net;
using Microsoft.Extensions.Logging;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/endpoint-response.test.ts</c> on the <c>endpoint-routing</c> fixture, whose sources are part of
/// <c>core-endpoints</c> (see <see cref="EndpointsFixture"/>). Upstream starts the dev server and the port requests the same routes from
/// the host, with the fixture built as <c>output: 'server'</c>, so the three tests are adapted: the endpoint runtime that answers is the
/// one of production, and the assertions are the upstream ones except where the list says so.
/// <list type="bullet">
/// <item>Headers with multiple values. <c>res.headers.get('x-triple')</c> of fetch joins the values with <c>", "</c>; the test joins what
/// the client has of the header, so that it does not depend on whether they arrive in one line or in three. <c>getSetCookie()</c> is the
/// list of <c>Set-Cookie</c> lines.</item>
/// <item>Can bail on streaming. The host buffers a response (the adapter renders with <c>streaming: false</c>), so the answer of
/// <c>/streaming</c> starts after its three seconds and the client that leaves after 500 ms has no response yet: the
/// request is cancelled, which is the <c>AbortError</c> that upstream expects when it expects anything (its <c>catch</c> has nothing that
/// fails when the request does not throw). The fixture records in <c>locals.cancelledByTheServer</c> whether the stream was cancelled and
/// no upstream test reads it, so it is not asserted. Not upstream: the host answers the next request, with no warning or error logged by
/// the one that was cancelled; the host of this test is its own for that, as the log is the one of the host.</item>
/// <item><c>/setCookies</c>: the cookies of the context and those of the headers are all in the response, as upstream asserts.</item>
/// </list>
/// </summary>
[Trait("Upstream", "core/endpoint-response.test.ts")]
public class EndpointResponseTests(SiteHost sites)
{
    [Fact(DisplayName = "endpoint responses: Headers with multiple values (set-cookie special case)")]
    public async Task Endpoint_HeadersWithMultipleValues_AreJoinedAndSetCookieIsKeptApart()
    {
        var site = await sites.GetAsync(EndpointsFixture.Name);

        using var response = await site.GetAsync("/multi-headers");

        Assert.Equal("single", Assert.Single(response.HeaderValues("x-single")));
        Assert.Equal("one, two, three", string.Join(", ", response.HeaderValues("x-triple")));
        var setCookies = response.HeaderValues("set-cookie");
        Assert.True(setCookies.Contains("hello"), "Should contain hello cookie");
        Assert.True(setCookies.Contains("world"), "Should contain world cookie");
    }

    [Fact(DisplayName = "endpoint responses: Can bail on streaming")]
    public async Task Streaming_WhenTheClientLeaves_IsCancelledAndTheHostKeepsAnswering()
    {
        var site = await sites.GetAsync(EndpointsFixture.Name, variant: "streaming-abort");
        // The host has an engine when the stream starts: the request that creates one would be cancelled before the endpoint ran.
        (await site.GetAsync("/multi-headers")).Dispose();

        using (var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(500)))
        {
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => site.Client.GetAsync("/streaming", cancellation.Token));
        }

        using var response = await site.GetAsync("/multi-headers");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.DoesNotContain(site.Logs, entry => entry.Level >= LogLevel.Warning);
    }

    [Fact(DisplayName = "endpoint responses: Accept setCookie from both context and headers")]
    public async Task Endpoint_SetCookie_FromTheContextAndFromTheHeaders_AreAllSent()
    {
        var site = await sites.GetAsync(EndpointsFixture.Name);

        using var response = await site.GetAsync("/setCookies");

        var setCookies = response.HeaderValues("set-cookie");
        foreach (var number in new[] { 1, 2, 3, 4 })
        {
            Assert.True(
                setCookies.Any(cookie => cookie.StartsWith($"key{number}=value{number}", StringComparison.Ordinal)),
                $"Should contain key{number} cookie");
        }
    }
}
