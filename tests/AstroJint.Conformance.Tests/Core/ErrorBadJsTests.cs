using System.Net;
using Microsoft.Extensions.Logging;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/error-bad-js.test.ts</c>. The fixture is <c>error-bad-js</c> of Astro (<c>core-error-bad-js</c>): a
/// page that imports a module which throws a <c>ReferenceError</c> when it is evaluated, and a page whose nested component throws one
/// while it renders. Both tests are adapted.
/// <list type="bullet">
/// <item>The <c>dev</c> test runs a dev server and finds <c>ReferenceError</c> in the page (the error overlay). The production build
/// answers 500 with an empty body, and the error goes to the log of the host (the console of the engine) as an error, which is where
/// the test looks for it. It requests the page twice, because the point of the test is that a failed page does not break the server
/// (upstream looks at the text of the first response twice), and then asks for a page that does not exist to see that the host still
/// answers.</item>
/// <item><c>in nested components, does not crash server</c> expects the stream of the response to fail while it is read, after the
/// start of the page has been sent. The adapter renders every page to the end before it sends anything (<c>streaming: false</c>), so
/// the same failure is a 500 response with no part of the page in it.</item>
/// </list>
/// </summary>
[Trait("Upstream", "core/error-bad-js.test.ts")]
public class ErrorBadJsTests(SiteHost sites)
{
    private const string Fixture = "core-error-bad-js";

    [Fact(DisplayName = "Errors in JavaScript: dev: Does not crash the dev server")]
    public async Task PageWithAFailingImport_Answers500AndTheHostKeepsServing()
    {
        var site = await sites.GetAsync(Fixture);
        var logStart = site.Logs.Count;

        using var first = await site.GetAsync("/");
        using var second = await site.GetAsync("/");
        using var unknown = await site.GetAsync("/does-not-exist");

        Assert.Equal(HttpStatusCode.InternalServerError, first.StatusCode);
        Assert.Equal(HttpStatusCode.InternalServerError, second.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Contains(site.Logs.Skip(logStart), entry => entry.Level >= LogLevel.Error && entry.Message.Contains("ReferenceError: bar is not defined"));
    }

    [Fact(DisplayName = "Errors in JavaScript: build: in nested components, does not crash server")]
    public async Task FailingNestedComponent_Answers500AndTheHostKeepsServing()
    {
        var site = await sites.GetAsync(Fixture);
        var logStart = site.Logs.Count;

        using var response = await site.GetAsync("/in-stream");
        var body = await response.ReadTextAsync();
        using var unknown = await site.GetAsync("/does-not-exist");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.DoesNotContain("<h1>Astro</h1>", body);
        Assert.Equal(HttpStatusCode.NotFound, unknown.StatusCode);
        Assert.Contains(site.Logs.Skip(logStart), entry => entry.Level >= LogLevel.Error && entry.Message.Contains("ReferenceError: somethingNotExists is not defined"));
    }
}
