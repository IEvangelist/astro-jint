using System.Net;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/error-page-cookies.test.ts</c>. The fixture is <c>error-page-cookies</c> of Astro
/// (<c>core-error-page-cookies</c>): a middleware that sets <c>sid=abc</c> and on-demand <c>404</c> and <c>500</c> pages that set a cookie
/// of their own. Upstream calls <c>app.render(request, { addCookieHeader: true })</c> and reads <c>response.headers.getSetCookie()</c>.
/// The adapter of this host always passes <c>addCookieHeader: true</c>, so the cookies of the app are real <c>Set-Cookie</c> headers
/// of the HTTP response, which the tests read the same way.
/// </summary>
[Trait("Upstream", "core/error-page-cookies.test.ts")]
public class ErrorPageCookiesTests(SiteHost sites)
{
    private const string Fixture = "core-error-page-cookies";

    [Fact(DisplayName = "Error page cookies: preserves cookies from the 500 error page when original page throws")]
    public async Task Error500Page_KeepsItsCookieWhenThePageThrows()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var setCookieHeaders = response.HeaderValues("Set-Cookie");
        Assert.True(setCookieHeaders.Any(cookie => cookie.Contains("flash=boom")), $"Expected flash=boom cookie, got: {string.Join(" | ", setCookieHeaders)}");
    }

    [Fact(DisplayName = "Error page cookies: preserves cookies from the 500 error page when no original cookies exist")]
    public async Task Error500Page_KeepsItsCookieWhenThePageSetNone()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/only-error-cookie");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var setCookieHeaders = response.HeaderValues("Set-Cookie");
        Assert.True(setCookieHeaders.Any(cookie => cookie.Contains("flash=boom")), $"Expected flash=boom cookie, got: {string.Join(" | ", setCookieHeaders)}");
    }

    [Fact(DisplayName = "Error page cookies: preserves cookies from both middleware and the 404 error page")]
    public async Task Error404Page_KeepsTheCookiesOfTheMiddlewareAndOfItsOwn()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var setCookieHeaders = response.HeaderValues("Set-Cookie");
        Assert.True(setCookieHeaders.Any(cookie => cookie.Contains("sid=abc")), $"Expected sid=abc cookie, got: {string.Join(" | ", setCookieHeaders)}");
        Assert.True(setCookieHeaders.Any(cookie => cookie.Contains("not_found=true")), $"Expected not_found=true cookie, got: {string.Join(" | ", setCookieHeaders)}");
    }
}
