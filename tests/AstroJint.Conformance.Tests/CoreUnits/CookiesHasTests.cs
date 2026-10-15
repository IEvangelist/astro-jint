namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/cookies/has.test.ts</c>: <c>Astro.cookies.has()</c> is true for a cookie of the request
/// and for one that was set, and false otherwise, also for the names of the properties of <c>Object.prototype</c>.
/// <para>
/// Differences from upstream (every test is adapted): the <c>cookie-ops</c> endpoint of the <c>app-cookies</c> fixture makes the
/// calls on the <c>Astro.cookies</c> of a real request that sends the <c>Cookie</c> header of the test, inside Jint
/// (<c>AppCookieOps</c>).
/// </para>
/// </summary>
[Trait("Upstream", "core-units/cookies/has.test.ts")]
public class CookiesHasTests(SiteHost sites)
{
    [Fact(DisplayName = "returns true if the request has the cookie")]
    public async Task Has_IsTrueForACookieOfTheRequest()
    {
        var run = await sites.RunCookieOpsAsync("foo=bar", new { op = "has", name = "foo" });

        Assert.True(run.Has(0));
    }

    [Fact(DisplayName = "returns false if the request does not have the cookie")]
    public async Task Has_IsFalseWhenTheRequestHasNoSuchCookie()
    {
        var run = await sites.RunCookieOpsAsync(null, new { op = "has", name = "foo" });

        Assert.False(run.Has(0));
    }

    [Fact(DisplayName = "returns true if the cookie has been set")]
    public async Task Has_IsTrueForACookieThatWasSet()
    {
        var run = await sites.RunCookieOpsAsync(
            null,
            new { op = "set", name = "foo", value = "bar" },
            new { op = "has", name = "foo" });

        Assert.True(run.Has(1));
    }

    [Fact(DisplayName = "returns false for Object.prototype properties when no cookie header is present")]
    public async Task Has_IsFalseForThePropertiesOfObjectPrototype()
    {
        var run = await sites.RunCookieOpsAsync(
            null,
            new { op = "has", name = "toString" },
            new { op = "has", name = "constructor" },
            new { op = "has", name = "hasOwnProperty" },
            new { op = "has", name = "valueOf" });

        for (var step = 0; step < 4; step++)
        {
            Assert.False(run.Has(step));
        }
    }
}
