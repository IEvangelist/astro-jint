using System.Text;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/cookies/get.test.ts</c>: <c>Astro.cookies.get()</c>, the decoding of the value (by
/// default <c>decodeURIComponent</c>, an undecodable value stays as it is, a custom <c>decode</c> option) and the conversions of
/// the cookie that it returns (<c>json()</c>, <c>number()</c>, <c>boolean()</c>).
/// <para>
/// Differences from upstream (every test is adapted): upstream builds an <c>AstroCookies</c> object around a hand-made
/// <c>Request</c>; here the request is a real one that sends the same <c>Cookie</c> header, and the <c>cookie-ops</c> endpoint of
/// the <c>app-cookies</c> fixture makes the same calls on the <c>Astro.cookies</c> of that request inside Jint and answers with
/// the results (<c>AppCookieOps</c>). The custom decoders are chosen by name in the step because a function does not cross HTTP:
/// <c>base64</c> (the decoder of 'gets the cookie value with custom decode', written in the endpoint because the host has no
/// <c>Buffer</c>) and a spy that answers the values it received.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/cookies/get.test.ts")]
public class CookiesGetTests(SiteHost sites)
{
    private const string Url = "http://localhost/?hello=world&foo=bar#hash";

    [Fact(DisplayName = "gets the cookie value")]
    public async Task Get_ReturnsTheValue()
    {
        var run = await sites.RunCookieOpsAsync("foo=bar", new { op = "get", name = "foo" });

        Assert.Equal("bar", run.Value(0));
    }

    [Theory(DisplayName = "gets an empty cookie value from")]
    [InlineData("foo=")]
    [InlineData("before=value; foo=; after=value")]
    public async Task Get_ReturnsAnEmptyValue(string header)
    {
        var run = await sites.RunCookieOpsAsync(header, new { op = "has", name = "foo" }, new { op = "get", name = "foo" });

        Assert.True(run.Has(0));
        Assert.Equal("", run.Value(1));
    }

    [Fact(DisplayName = "passes an empty cookie value to a custom decoder")]
    public async Task Get_PassesAnEmptyValueToTheDecoder()
    {
        var run = await sites.RunCookieOpsAsync("foo=", new { op = "get", name = "foo", decode = "record-empty" });

        Assert.Equal("decoded empty value", run.Value(0));
        Assert.Equal([""], run.Result(0).Strings("decoded"));
    }

    [Fact(DisplayName = "gets the cookie value with default decode")]
    public async Task Get_DecodesTheValueByDefault()
    {
        var run = await sites.RunCookieOpsAsync($"url={Uri.EscapeDataString(Url)}", new { op = "get", name = "url" });

        // By default decodeURIComponent is used on the value.
        Assert.Equal(Url, run.Value(0));
    }

    [Fact(DisplayName = "gets the cookie value with custom decode")]
    public async Task Get_DecodesTheValueWithTheDecoderOfTheOptions()
    {
        var encoded = Convert.ToBase64String(Encoding.UTF8.GetBytes(Url));

        var run = await sites.RunCookieOpsAsync(
            $"url={encoded}",
            new { op = "has", name = "url" },
            new { op = "get", name = "url", decode = "base64" },
            new { op = "get", name = "url" });

        Assert.True(run.Has(0));
        Assert.Equal(Url, run.Value(1));
        Assert.Equal(encoded, run.Value(2));
    }

    [Fact(DisplayName = "Returns undefined is the value doesn't exist")]
    public async Task Get_ReturnsNothingWhenThereIsNoCookie()
    {
        var run = await sites.RunCookieOpsAsync(null, new { op = "get", name = "foo" });

        Assert.Null(run.Value(0));
    }

    [Fact(DisplayName = "does not return values from Object.prototype when no cookie header is present")]
    public async Task Get_DoesNotReturnPropertiesOfObjectPrototype()
    {
        var run = await sites.RunCookieOpsAsync(
            null,
            new { op = "get", name = "toString" },
            new { op = "get", name = "constructor" },
            new { op = "get", name = "hasOwnProperty" },
            new { op = "get", name = "valueOf" });

        for (var step = 0; step < 4; step++)
        {
            Assert.Null(run.Value(step));
        }
    }

    [Fact(DisplayName = "handles malformed cookie values gracefully")]
    public async Task Get_ReturnsAMalformedValueUndecoded()
    {
        // An incomplete percent-encoding makes decodeURIComponent throw: the value is returned as it came.
        var run = await sites.RunCookieOpsAsync("malformed=0:%", new { op = "get", name = "malformed" });

        Assert.Equal("0:%", run.Value(0));
    }

    [Fact(DisplayName = "returns a JavaScript object")]
    public async Task Json_ReturnsAnObject()
    {
        var run = await sites.RunCookieOpsAsync("foo=%7B%22key%22%3A%22value%22%7D", new { op = "get", name = "foo", @as = "json" });

        var result = run.Result(0);
        Assert.Equal("object", result.Str("type"));
        Assert.Equal("value", result.GetProperty("json").Str("key"));
    }

    [Fact(DisplayName = "Coerces into a number")]
    public async Task Number_CoercesIntoANumber()
    {
        var run = await sites.RunCookieOpsAsync("foo=22", new { op = "get", name = "foo", @as = "number" });

        var result = run.Result(0);
        Assert.Equal("number", result.Str("type"));
        Assert.Equal(22, result.GetProperty("number").GetInt32());
    }

    [Fact(DisplayName = "Coerces non-number into NaN")]
    public async Task Number_CoercesANonNumberIntoNaN()
    {
        var run = await sites.RunCookieOpsAsync("foo=bar", new { op = "get", name = "foo", @as = "number" });

        var result = run.Result(0);
        Assert.Equal("number", result.Str("type"));
        Assert.True(result.Bool("isNaN"));
    }

    [Fact(DisplayName = "Coerces true into `true`")]
    public async Task Boolean_CoercesTrue() => await AssertBooleanAsync("foo=true", expected: true);

    [Fact(DisplayName = "Coerces false into `false`")]
    public async Task Boolean_CoercesFalse() => await AssertBooleanAsync("foo=false", expected: false);

    [Fact(DisplayName = "Coerces 1 into `true`")]
    public async Task Boolean_CoercesOne() => await AssertBooleanAsync("foo=1", expected: true);

    [Fact(DisplayName = "Coerces 0 into `false`")]
    public async Task Boolean_CoercesZero() => await AssertBooleanAsync("foo=0", expected: false);

    [Fact(DisplayName = "Coerces truthy strings into `true`")]
    public async Task Boolean_CoercesATruthyString() => await AssertBooleanAsync("foo=bar", expected: true);

    private async Task AssertBooleanAsync(string cookie, bool expected)
    {
        var run = await sites.RunCookieOpsAsync(cookie, new { op = "get", name = "foo", @as = "boolean" });

        var result = run.Result(0);
        Assert.Equal("boolean", result.Str("type"));
        Assert.Equal(expected, result.Bool("boolean"));
    }
}
