using System.Globalization;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/cookies/delete.test.ts</c>: <c>Astro.cookies.delete()</c> adds a <c>Set-Cookie</c> line
/// that expires the cookie (value <c>deleted</c>, <c>Expires</c> in 1970), keeps the attributes of the options except
/// <c>expires</c> and <c>maxAge</c>, and makes <c>get()</c> and <c>has()</c> answer as if the cookie did not exist.
/// <para>
/// Differences from upstream (every test is adapted): the <c>cookie-ops</c> endpoint of the <c>app-cookies</c> fixture makes the
/// calls on the <c>Astro.cookies</c> of a real request inside Jint (<c>AppCookieOps</c>) and the tests read the <c>Set-Cookie</c>
/// lines of the response instead of <c>cookies.headers()</c>. A <c>Date</c> does not cross HTTP: the <c>expires</c> option is sent
/// as an ISO date and the endpoint turns it into a <c>Date</c>.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/cookies/delete.test.ts")]
public class CookiesDeleteTests(SiteHost sites)
{
    private const string Expired = "Expires=Thu, 01 Jan 1970 00:00:00 GMT";

    [Fact(DisplayName = "creates a Set-Cookie header to delete it")]
    public async Task Delete_AddsASetCookieLine()
    {
        var run = await sites.RunCookieOpsAsync(
            "foo=bar",
            new { op = "get", name = "foo" },
            new { op = "delete", name = "foo" });

        Assert.Equal("bar", run.Value(0));
        Assert.Single(run.SetCookies);
    }

    [Fact(DisplayName = "calling cookies.get() after returns undefined")]
    public async Task Get_AfterDelete_ReturnsNothing()
    {
        var run = await sites.RunCookieOpsAsync(
            "foo=bar",
            new { op = "get", name = "foo" },
            new { op = "delete", name = "foo" },
            new { op = "get", name = "foo" });

        Assert.Equal("bar", run.Value(0));
        Assert.Null(run.Value(2));
    }

    [Fact(DisplayName = "calling cookies.has() after returns false")]
    public async Task Has_AfterDelete_ReturnsFalse()
    {
        var run = await sites.RunCookieOpsAsync(
            "foo=bar",
            new { op = "has", name = "foo" },
            new { op = "delete", name = "foo" },
            new { op = "has", name = "foo" });

        Assert.True(run.Has(0));
        Assert.False(run.Has(2));
    }

    [Fact(DisplayName = "deletes a cookie with attributes")]
    public async Task Delete_KeepsTheAttributes()
    {
        var run = await sites.RunCookieOpsAsync(
            null,
            new
            {
                op = "delete",
                name = "foo",
                options = new
                {
                    domain = "example.com",
                    path = "/subpath/",
                    priority = "high",
                    secure = true,
                    httpOnly = true,
                    sameSite = "strict",
                },
            });

        var line = Assert.Single(run.SetCookies);
        Assert.Contains("foo=deleted", line, StringComparison.Ordinal);
        Assert.Contains(Expired, line, StringComparison.Ordinal);
        Assert.Contains("Domain=example.com", line, StringComparison.Ordinal);
        Assert.Contains("Path=/subpath/", line, StringComparison.Ordinal);
        Assert.Contains("Priority=High", line, StringComparison.Ordinal);
        Assert.Contains("Secure", line, StringComparison.Ordinal);
        Assert.Contains("HttpOnly", line, StringComparison.Ordinal);
        Assert.Contains("SameSite=Strict", line, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "ignores expires option")]
    public async Task Delete_IgnoresTheExpiresOption()
    {
        var now = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture);

        var run = await sites.RunCookieOpsAsync(null, new { op = "delete", name = "foo", options = new { expires = now } });

        var line = Assert.Single(run.SetCookies);
        Assert.Contains("foo=deleted", line, StringComparison.Ordinal);
        Assert.Contains(Expired, line, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "ignores maxAge option")]
    public async Task Delete_IgnoresTheMaxAgeOption()
    {
        var run = await sites.RunCookieOpsAsync(null, new { op = "delete", name = "foo", options = new { maxAge = 60 } });

        var line = Assert.Single(run.SetCookies);
        Assert.Contains("foo=deleted", line, StringComparison.Ordinal);
        Assert.Contains(Expired, line, StringComparison.Ordinal);
    }
}
