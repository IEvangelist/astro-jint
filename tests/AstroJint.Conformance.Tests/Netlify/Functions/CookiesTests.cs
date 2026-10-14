using System.Net;

namespace AstroJint.Conformance.Netlify;

/// <summary>
/// Port of <c>packages/integrations/netlify/test/functions/cookies.test.ts</c>. Upstream imports the generated Netlify function
/// and calls it with a <c>Request</c>; here the same requests go over HTTP to the host, which renders them in Jint. The
/// cookies fixture of upstream shares one project (<c>netlify-functions</c>) with the middleware fixture, which does not change
/// what these requests answer.
/// </summary>
[Trait("Upstream", "netlify/functions/cookies.test.ts")]
public class CookiesTests(SiteHost sites)
{
    private const string Fixture = "netlify-functions";

    [Fact(DisplayName = "Can set multiple")]
    public async Task Cookies_CanSetMultiple()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.SendAsync(HttpMethod.Post, "/login", new StringContent("{}"));

        Assert.Equal(HttpStatusCode.MovedPermanently, response.StatusCode);
        Assert.Equal(["/"], response.HeaderValues("Location"));
        Assert.Equal(["foo=foo; HttpOnly", "bar=bar; HttpOnly"], response.HeaderValues("Set-Cookie"));
    }

    [Fact(DisplayName = "Can set partitioned cookie")]
    public async Task Cookies_CanSetPartitionedCookie()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/partitioned");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var cookies = response.HeaderValues("Set-Cookie");
        Assert.NotEmpty(cookies);
        Assert.Contains("Partitioned", cookies[0]);
    }

    [Fact(DisplayName = "renders dynamic 404 page")]
    public async Task NotFound_RendersTheDynamic404Page()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.SendAsync(HttpMethod.Get, "/nonexistant-page", headers: ("x-test", "bar"));
        var text = await response.ReadTextAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("This is my custom 404 page", text);
        Assert.Contains("x-test: bar", text);
    }
}
