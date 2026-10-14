using System.Net;

namespace AstroJint.Sample;

/// <summary>What ASP.NET Core knows about the visitor reaches Astro through the host: <c>Astro.session</c> and <c>Astro.locals</c>.</summary>
public class SessionAndIdentityTests(SampleSite site)
{
    private static async Task<int> VisitAsync(HttpClient client) =>
        int.Parse((await (await client.GetAsync("/session")).ReadHtmlAsync()).Text("#visits"));

    private static async Task<string> WhoAmIAsync(HttpClient client) =>
        (await (await client.GetAsync("/me")).ReadHtmlAsync()).Text("#user");

    [Fact]
    public async Task ASessionCountsTheVisitsOfOneBrowser()
    {
        using var client = site.CreateClient(cookies: true, https: true);

        // The pool has two engines, so the visits are rendered by both of them: the data is not in an engine.
        var visits = new List<int>();
        for (var visit = 0; visit < 6; visit++)
        {
            visits.Add(await VisitAsync(client));
        }

        Assert.Equal([1, 2, 3, 4, 5, 6], visits);
    }

    [Fact]
    public async Task EveryBrowserHasItsOwnSession()
    {
        using var first = site.CreateClient(cookies: true, https: true);
        using var second = site.CreateClient(cookies: true, https: true);

        Assert.Equal(1, await VisitAsync(first));
        Assert.Equal(2, await VisitAsync(first));
        Assert.Equal(1, await VisitAsync(second));
        Assert.Equal(3, await VisitAsync(first));
        Assert.Equal(2, await VisitAsync(second));
    }

    [Fact]
    public async Task TheSessionCookieIsNotReadableByScriptsAndOnlyTravelsOverHttps()
    {
        using var client = site.CreateClient();

        using var response = await client.GetAsync("/session");

        var cookie = Assert.Single(response.HeaderValues("Set-Cookie"), header => header.StartsWith("astro-session=", StringComparison.Ordinal));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("secure", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AnAnonymousVisitorIsAnonymousAndTheTraceIdIsTheOneOfTheRequest()
    {
        using var client = site.CreateClient();

        using var response = await client.GetAsync("/me");
        var page = await response.ReadHtmlAsync();

        Assert.StartsWith("Anonymous", page.Text("#user"));
        Assert.Equal(response.Header("X-Request-Id"), page.Text("#trace-id"));
    }

    [Fact]
    public async Task TheCookieOfAspNetCoreSignsTheVisitorInForAstro()
    {
        using var client = site.CreateClient(cookies: true);

        using var signIn = await client.GetAsync("/sign-in/ada");
        var signedIn = await (await client.GetAsync("/me")).ReadHtmlAsync();
        using var signOut = await client.GetAsync("/sign-out");

        Assert.Equal(HttpStatusCode.Found, signIn.StatusCode);
        Assert.Equal("/me", signIn.Headers.Location?.OriginalString);
        Assert.Equal("ada", signedIn.Text("#user strong"));
        Assert.StartsWith("Anonymous", await WhoAmIAsync(client));
    }

    [Fact]
    public async Task TheLocalsOfOneRequestDoNotReachAnother()
    {
        using var ada = site.CreateClient(cookies: true);
        using var anonymous = site.CreateClient();
        using var signIn = await ada.GetAsync("/sign-in/ada");

        // Both engines of the pool serve both visitors, at the same time.
        var requests = Enumerable.Range(0, 12).Select(index => index % 2 == 0 ? WhoAmIAsync(ada) : WhoAmIAsync(anonymous)).ToArray();
        var answers = await Task.WhenAll(requests);

        for (var index = 0; index < answers.Length; index++)
        {
            if (index % 2 == 0)
            {
                Assert.StartsWith("Signed in as ada", answers[index]);
            }
            else
            {
                Assert.StartsWith("Anonymous", answers[index]);
            }
        }
    }

    [Fact]
    public async Task EveryRequestHasItsOwnTraceId()
    {
        using var client = site.CreateClient();

        var pages = await Task.WhenAll(Enumerable.Range(0, 8).Select(async _ => await (await client.GetAsync("/me")).ReadHtmlAsync()));

        Assert.Equal(8, pages.Select(page => page.Text("#trace-id")).Distinct().Count());
    }
}
