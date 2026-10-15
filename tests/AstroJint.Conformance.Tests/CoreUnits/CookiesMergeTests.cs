namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/cookies/merge.test.ts</c>: <c>AstroCookies.merge()</c> (the cookies of the other object
/// are added, a clash is won by the other object), <c>AstroCookies.headers()</c> (one string per cookie that was set, none when
/// nothing was set) and the pair <c>attachCookiesToResponse</c> / <c>getSetCookiesFromResponse</c> that carries the cookies on
/// a <c>Response</c>.
/// <para>
/// Differences from upstream (every test is adapted): the <c>cookie-ops</c> endpoint of the <c>app-cookies</c> fixture runs the
/// calls inside Jint (<c>AppCookieOps</c>). The target of <c>merge()</c> is the <c>Astro.cookies</c> of the real request and the
/// source is a second <c>AstroCookies</c> object that the endpoint creates, so the merged cookies are the <c>Set-Cookie</c> lines
/// of the response; the <c>headers</c> step answers <c>cookies.headers()</c> at that point of the steps. The two
/// <c>roundtrip</c> tests attach a second object to a <c>new Response(null)</c> (or none) and read it back with
/// <c>getSetCookiesFromResponse</c>, as upstream does, with the module <c>core/cookies/response.js</c> imported by relative path
/// from the fixture because it is not an export of the package.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/cookies/merge.test.ts")]
public class CookiesMergeTests(SiteHost sites)
{
    [Fact(DisplayName = "copies all cookies from source into an empty target")]
    public async Task Merge_CopiesTheCookiesIntoAnEmptyTarget()
    {
        var run = await sites.RunCookieOpsAsync(
            null,
            new
            {
                op = "merge",
                source = new object[]
                {
                    new { op = "set", name = "foo", value = "bar" },
                    new { op = "set", name = "baz", value = "qux" },
                },
            });

        Assert.Equal(2, run.SetCookies.Count);
        Assert.Contains(run.SetCookies, line => line.StartsWith("foo=", StringComparison.Ordinal));
        Assert.Contains(run.SetCookies, line => line.StartsWith("baz=", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "overwrites same-key cookies in target")]
    public async Task Merge_OverwritesTheSameKeyInTheTarget()
    {
        var run = await sites.RunCookieOpsAsync(
            null,
            new { op = "set", name = "foo", value = "old" },
            new { op = "merge", source = new object[] { new { op = "set", name = "foo", value = "new" } } });

        var line = Assert.Single(run.SetCookies);
        Assert.StartsWith("foo=new", line, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "preserves non-conflicting keys from target")]
    public async Task Merge_PreservesTheOtherKeysOfTheTarget()
    {
        var run = await sites.RunCookieOpsAsync(
            null,
            new { op = "set", name = "b", value = "2" },
            new { op = "merge", source = new object[] { new { op = "set", name = "a", value = "1" } } });

        Assert.Equal(2, run.SetCookies.Count);
        Assert.Contains(run.SetCookies, line => line.StartsWith("a=", StringComparison.Ordinal));
        Assert.Contains(run.SetCookies, line => line.StartsWith("b=", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "is a no-op when source has no outgoing cookies")]
    public async Task Merge_ChangesNothingWhenTheSourceSetNothing()
    {
        var run = await sites.RunCookieOpsAsync(
            null,
            new { op = "set", name = "foo", value = "bar" },
            new { op = "merge", source = Array.Empty<object>() });

        var line = Assert.Single(run.SetCookies);
        Assert.StartsWith("foo=", line, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "yields nothing when no cookies have been set")]
    public async Task Headers_YieldsNothingWhenNothingWasSet()
    {
        var run = await sites.RunCookieOpsAsync(null, new { op = "headers" });

        Assert.Empty(run.Result(0).Strings("headers"));
        Assert.Empty(run.SetCookies);
    }

    [Fact(DisplayName = "yields one header string per set cookie")]
    public async Task Headers_YieldsOneStringPerCookie()
    {
        var run = await sites.RunCookieOpsAsync(
            null,
            new { op = "set", name = "a", value = "1" },
            new { op = "set", name = "b", value = "2" },
            new { op = "set", name = "c", value = "3" },
            new { op = "headers" });

        Assert.Equal(3, run.Result(3).Strings("headers").Length);
        Assert.Equal(3, run.SetCookies.Count);
    }

    [Fact(DisplayName = "roundtrip: attached cookies are readable from the response")]
    public async Task Roundtrip_ReadsTheAttachedCookiesFromTheResponse()
    {
        var run = await sites.RunCookieOpsAsync(
            null,
            new
            {
                op = "roundtrip",
                attach = true,
                source = new object[]
                {
                    new { op = "set", name = "session", value = "abc" },
                    new { op = "set", name = "theme", value = "dark" },
                },
            });

        var setCookies = run.Result(0).Strings("setCookies");
        Assert.Equal(2, setCookies.Length);
        Assert.Contains(setCookies, line => line.StartsWith("session=", StringComparison.Ordinal));
        Assert.Contains(setCookies, line => line.StartsWith("theme=", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "getSetCookiesFromResponse returns empty when no cookies attached")]
    public async Task Roundtrip_ReturnsNothingWhenNoCookiesWereAttached()
    {
        var run = await sites.RunCookieOpsAsync(null, new { op = "roundtrip", attach = false, source = Array.Empty<object>() });

        Assert.Empty(run.Result(0).Strings("setCookies"));
    }
}
