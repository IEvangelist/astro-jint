using System.Globalization;

namespace AstroJint.Conformance.Node;

/// <summary>
/// Port of <c>packages/integrations/node/test/headers.test.ts</c>. Upstream compares the headers the adapter set on a mock
/// response; here the headers are read off the wire, so the ones Kestrel adds to every response are left out.
/// </summary>
[Trait("Upstream", "node/headers.test.ts")]
public class HeadersTests(SiteHost sites)
{
    private const string Fixture = "headers";

    private static readonly string[] TransportHeaders = ["content-length", "date", "server", "transfer-encoding", "connection", "keep-alive"];

    [Fact(DisplayName = "Endpoint Simple Headers")]
    public Task EndpointSimpleHeaders() => RunTest(
        "/endpoints/simple",
        "content-type: text/plain;charset=utf-8",
        "x-hello: world");

    [Fact(DisplayName = "Endpoint Astro Single Cookie Header")]
    public Task EndpointAstroSingleCookieHeader() => RunTest(
        "/endpoints/astro-cookies-single",
        "content-type: text/plain;charset=utf-8",
        "set-cookie: from1=astro1");

    [Fact(DisplayName = "Endpoint Astro Multi Cookie Header")]
    public Task EndpointAstroMultiCookieHeader() => RunTest(
        "/endpoints/astro-cookies-multi",
        "content-type: text/plain;charset=utf-8",
        "set-cookie: from1=astro1",
        "set-cookie: from2=astro2");

    [Fact(DisplayName = "Endpoint Response Single Cookie Header")]
    public Task EndpointResponseSingleCookieHeader() => RunTest(
        "/endpoints/response-cookies-single",
        "content-type: text/plain;charset=utf-8",
        "set-cookie: hello1=world1");

    [Fact(DisplayName = "Endpoint Response Multi Cookie Header")]
    public Task EndpointResponseMultiCookieHeader() => RunTest(
        "/endpoints/response-cookies-multi",
        "content-type: text/plain;charset=utf-8",
        "set-cookie: hello1=world1",
        "set-cookie: hello2=world2");

    [Fact(DisplayName = "Endpoint Complex Headers Kitchen Sink")]
    public Task EndpointComplexHeadersKitchenSink() => RunTest(
        "/endpoints/kitchen-sink",
        "content-type: text/plain;charset=utf-8",
        "x-single: single",
        "x-triple: one, two, three",
        "set-cookie: hello1=world1",
        "set-cookie: hello2=world2");

    [Fact(DisplayName = "Endpoint Astro and Response Single Cookie Header")]
    public Task EndpointAstroAndResponseSingleCookieHeader() => RunTest(
        "/endpoints/astro-response-cookie-single",
        "content-type: text/plain;charset=utf-8",
        "set-cookie: from1=response1",
        "set-cookie: from1=astro1");

    [Fact(DisplayName = "Endpoint Astro and Response Multi Cookie Header")]
    public Task EndpointAstroAndResponseMultiCookieHeader() => RunTest(
        "/endpoints/astro-response-cookie-multi",
        "content-type: text/plain;charset=utf-8",
        "set-cookie: from1=response1",
        "set-cookie: from2=response2",
        "set-cookie: from3=astro1",
        "set-cookie: from4=astro2");

    [Fact(DisplayName = "Endpoint Response Empty Headers Object")]
    public Task EndpointResponseEmptyHeadersObject() => RunTest(
        "/endpoints/response-empty-headers-object",
        "content-type: text/plain;charset=UTF-8");

    [Fact(DisplayName = "Endpoint Response undefined Headers Object")]
    public Task EndpointResponseUndefinedHeadersObject() => RunTest(
        "/endpoints/response-undefined-headers-object",
        "content-type: text/plain;charset=UTF-8");

    [Fact(DisplayName = "Component Astro Single Cookie Header")]
    public Task ComponentAstroSingleCookieHeader() => RunTest(
        "/astro/component-astro-cookies-single",
        "content-type: text/html",
        "set-cookie: from1=astro1");

    [Fact(DisplayName = "Component Astro Multi Cookie Header")]
    public Task ComponentAstroMultiCookieHeader() => RunTest(
        "/astro/component-astro-cookies-multi",
        "content-type: text/html",
        "set-cookie: from1=astro1",
        "set-cookie: from2=astro2");

    [Fact(DisplayName = "Component Response Single Cookie Header")]
    public Task ComponentResponseSingleCookieHeader() => RunTest(
        "/astro/component-response-cookies-single",
        "content-type: text/html",
        "set-cookie: from1=value1");

    [Fact(DisplayName = "Component Response Multi Cookie Header")]
    public Task ComponentResponseMultiCookieHeader() => RunTest(
        "/astro/component-response-cookies-multi",
        "content-type: text/html",
        "set-cookie: from1=value1",
        "set-cookie: from2=value2");

    [Fact(DisplayName = "Component Astro and Response Single Cookie Header")]
    public Task ComponentAstroAndResponseSingleCookieHeader() => RunTest(
        "/astro/component-astro-response-cookie-single",
        "content-type: text/html",
        "set-cookie: from1=response1",
        "set-cookie: from1=astro1");

    [Fact(DisplayName = "Component Astro and Response Multi Cookie Header")]
    public Task ComponentAstroAndResponseMultiCookieHeader() => RunTest(
        "/astro/component-astro-response-cookie-multi",
        "content-type: text/html",
        "set-cookie: from1=response1",
        "set-cookie: from2=response2",
        "set-cookie: from3=astro1",
        "set-cookie: from4=astro2");

    // The Node adapter streams by default and sends the 'component-simple' page in several chunks; the host buffers the whole
    // response, so this is the counterpart of its 'without streaming' test: one body with a known length.
    [Fact(DisplayName = "sends a single chunk")]
    public async Task BufferedResponses_AreSentAsOneBodyWithALength()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/astro/component-simple");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(response.Body.Length.ToString(CultureInfo.InvariantCulture), response.Header("Content-Length"));
        Assert.Null(response.Header("Transfer-Encoding"));
        Assert.Contains("<p>hello world</p>", response.Text);
    }

    private async Task RunTest(string path, params string[] expectedHeaders)
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync(path);

        Assert.Equal(200, response.StatusCode);
        var actual = Normalize(response.Headers.Where(header => !TransportHeaders.Contains(header.Key, StringComparer.OrdinalIgnoreCase)));
        var expected = Normalize(expectedHeaders.Select(line =>
        {
            var separator = line.IndexOf(": ", StringComparison.Ordinal);
            return KeyValuePair.Create(line[..separator], line[(separator + 2)..]);
        }));
        Assert.Equal(expected, actual);
    }

    // One entry per header name, in alphabetical order; repeated headers keep the order of their values.
    private static string[] Normalize(IEnumerable<KeyValuePair<string, string>> headers) =>
        [.. headers
            .GroupBy(header => header.Key.ToLowerInvariant())
            .OrderBy(group => group.Key, StringComparer.Ordinal)
            .Select(group => $"{group.Key}: {string.Join(" | ", group.Select(header => header.Value))}")];
}
