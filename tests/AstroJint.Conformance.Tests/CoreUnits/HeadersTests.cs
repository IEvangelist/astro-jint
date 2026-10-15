namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/app/headers.test.ts</c>: <c>createOutgoingHttpHeaders()</c>, the function of
/// <c>astro/app/node</c> that turns the <c>Headers</c> of a response into the headers object of Node's <c>ServerResponse</c>: one
/// key per header, the values of a repeated header joined with <c>", "</c>, except <c>Set-Cookie</c>, whose values stay apart
/// (whatever the case of its name).
/// <para>
/// Six tests are adapted: the host has no such function (it copies the headers of the response of the engine to the response of
/// ASP.NET Core), so the endpoint <c>outgoing-headers</c> of the <c>app-pages</c> fixture builds the <c>Headers</c> of each test
/// and answers with them, and the tests read what the client receives: a repeated header has the values of upstream's result
/// when they are joined with <c>", "</c> (HTTP lets a sender repeat a header line instead of joining it), and <c>Set-Cookie</c>
/// arrives as one header line per value.
/// </para>
/// <para>
/// Not applicable: the three tests that pass <c>undefined</c>, <c>null</c> and an empty <c>Headers</c> to the function and expect
/// <c>undefined</c> back ('undefined input headers', 'null input headers', 'Empty Headers'): they check the return value of a
/// Node-specific converter, and a response without headers is what every test that answers with a bare body already sends.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/app/headers.test.ts")]
public class HeadersTests(SiteHost sites)
{
    private const string Fixture = "app-pages";

    [Fact(DisplayName = "Headers with single key")]
    public async Task SingleKey_IsSent()
    {
        var response = await GetAsync("single-key");

        Assert.Equal(["hello world"], response.HeaderValues("x-test"));
    }

    [Fact(DisplayName = "Headers with multiple keys")]
    public async Task MultipleKeys_AreSent()
    {
        var response = await GetAsync("multiple-keys");

        Assert.Equal(["hello"], response.HeaderValues("x-test1"));
        Assert.Equal(["world"], response.HeaderValues("x-test2"));
    }

    [Fact(DisplayName = "Headers with multiple values (not set-cookie)")]
    public async Task MultipleValues_AreJoined()
    {
        var response = await GetAsync("multiple-values");

        Assert.Equal("hello, world", string.Join(", ", response.HeaderValues("x-test")));
    }

    [Fact(DisplayName = "Headers with multiple values (set-cookie special case)")]
    public async Task SetCookie_MultipleValues_StayApart()
    {
        var response = await GetAsync("set-cookie");

        Assert.Equal(["hello", "world"], response.HeaderValues("set-cookie"));
    }

    [Fact(DisplayName = "Headers with multiple values (set-cookie case handling)")]
    public async Task SetCookie_WhateverTheCase_StaysApart()
    {
        var response = await GetAsync("set-cookie-case");

        Assert.Equal(["hello", "world"], response.HeaderValues("set-cookie"));
    }

    [Fact(DisplayName = "Headers with all use cases")]
    public async Task AllUseCases_AreSent()
    {
        var response = await GetAsync("all");

        Assert.Equal(["single"], response.HeaderValues("x-single"));
        Assert.Equal("one, two, three", string.Join(", ", response.HeaderValues("x-triple")));
        Assert.Equal(["hello", "world"], response.HeaderValues("set-cookie"));
    }

    private async Task<RawResponse> GetAsync(string testCase)
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync($"/outgoing-headers?case={testCase}");

        Assert.Equal(200, response.StatusCode);
        return response;
    }
}
