using System.Text.Json;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/cookies/set.test.ts</c>: <c>Astro.cookies.set()</c>, the <c>Set-Cookie</c> line that it
/// produces (encoding of the value, cookie options, objects, numbers and booleans as values) and what <c>get()</c> answers after
/// a <c>set()</c>.
/// <para>
/// Differences from upstream (every test is adapted): upstream reads <c>cookies.headers()</c> of a hand-made <c>AstroCookies</c>
/// object; here the <c>cookie-ops</c> endpoint of the <c>app-cookies</c> fixture makes the same calls on the <c>Astro.cookies</c> of
/// a real request inside Jint, and the tests read the <c>Set-Cookie</c> lines of the response (<c>AppCookieOps</c> checks that they
/// are the strings of <c>cookies.headers()</c>). The custom encoder is chosen by name (<c>identity</c>) because a function does
/// not cross HTTP.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/cookies/set.test.ts")]
public class CookiesSetTests(SiteHost sites)
{
    private const string Url = "http://localhost/path";

    [Fact(DisplayName = "Sets a cookie value that can be serialized")]
    public async Task Set_SerializesAValue()
    {
        var run = await sites.RunCookieOpsAsync(null, new { op = "set", name = "foo", value = "bar" });

        Assert.Equal(["foo=bar"], run.SetCookies);
    }

    [Fact(DisplayName = "Sets a cookie value without encoding when it only contains cookie-safe characters")]
    public async Task Set_DoesNotEncodeCookieSafeCharacters()
    {
        var run = await sites.RunCookieOpsAsync(null, new { op = "set", name = "url", value = Url });

        // Cookie-safe values are emitted as they are by default.
        Assert.Equal([$"url={Url}"], run.SetCookies);
    }

    [Fact(DisplayName = "Sets a cookie value that is URI encoded when it contains unsafe characters")]
    public async Task Set_EncodesUnsafeCharacters()
    {
        var run = await sites.RunCookieOpsAsync(null, new { op = "set", name = "greeting", value = "hello world" });

        Assert.Equal([$"greeting={Uri.EscapeDataString("hello world")}"], run.SetCookies);
    }

    [Fact(DisplayName = "Sets a cookie value that can be serialized w/ custom encode behavior")]
    public async Task Set_UsesTheEncoderOfTheOptions()
    {
        // The identity function as the encode option: no longer URI encoded.
        var run = await sites.RunCookieOpsAsync(null, new { op = "set", name = "url", value = Url, encode = "identity" });

        Assert.Equal([$"url={Url}"], run.SetCookies);
    }

    [Fact(DisplayName = "Can set cookie options")]
    public async Task Set_AppliesTheOptions()
    {
        var run = await sites.RunCookieOpsAsync(
            null,
            new { op = "set", name = "foo", value = "bar", options = new { httpOnly = true, path = "/subpath/" } });

        Assert.Equal(["foo=bar; Path=/subpath/; HttpOnly"], run.SetCookies);
    }

    [Fact(DisplayName = "Can pass a JavaScript object that will be serialized")]
    public async Task Set_SerializesAnObject()
    {
        var run = await sites.RunCookieOpsAsync(null, new { op = "set", name = "options", value = new { one = "two", three = 4 } });

        var line = Assert.Single(run.SetCookies);
        Assert.StartsWith("options=", line, StringComparison.Ordinal);
        var value = JsonDocument.Parse(Uri.UnescapeDataString(line["options=".Length..])).RootElement;
        Assert.Equal("two", value.Str("one"));
    }

    [Fact(DisplayName = "Can pass a number")]
    public async Task Set_SerializesANumber()
    {
        var run = await sites.RunCookieOpsAsync(null, new { op = "set", name = "one", value = 2 });

        Assert.Equal(["one=2"], run.SetCookies);
    }

    [Fact(DisplayName = "Can pass a boolean")]
    public async Task Set_SerializesABoolean()
    {
        var run = await sites.RunCookieOpsAsync(
            null,
            new { op = "set", name = "admin", value = true },
            new { op = "get", name = "admin", @as = "boolean" });

        Assert.True(run.Result(1).Bool("boolean"));
        Assert.Equal(["admin=true"], run.SetCookies);
    }

    [Fact(DisplayName = "Can get the value after setting")]
    public async Task Get_ReturnsTheValueThatWasSet()
    {
        var run = await sites.RunCookieOpsAsync(
            null,
            new { op = "set", name = "foo", value = "bar" },
            new { op = "get", name = "foo" });

        Assert.Equal("bar", run.Value(1));
    }

    [Fact(DisplayName = "Can get the JavaScript object after setting")]
    public async Task Get_ReturnsTheObjectThatWasSet()
    {
        var run = await sites.RunCookieOpsAsync(
            null,
            new { op = "set", name = "options", value = new { one = "two", three = 4 } },
            new { op = "get", name = "options", @as = "json" });

        var result = run.Result(1);
        Assert.Equal("object", result.Str("type"));
        var value = result.GetProperty("json");
        Assert.Equal("two", value.Str("one"));
        Assert.Equal(JsonValueKind.Number, value.GetProperty("three").ValueKind);
        Assert.Equal(4, value.GetProperty("three").GetInt32());
    }

    [Fact(DisplayName = "Overrides a value in the request")]
    public async Task Set_OverridesTheValueOfTheRequest()
    {
        var run = await sites.RunCookieOpsAsync(
            "foo=bar",
            new { op = "get", name = "foo" },
            new { op = "set", name = "foo", value = "baz" },
            new { op = "get", name = "foo" });

        Assert.Equal("bar", run.Value(0));
        Assert.Equal("baz", run.Value(2));
    }
}
