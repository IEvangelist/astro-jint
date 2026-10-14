using System.Text;
using System.Text.Json;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/app/ssr-api-route.test.ts</c>: on-demand endpoints answer GET, POST and HEAD, hand
/// their handler a correct API context, read multipart bodies with binary files, and keep the status, status text and headers
/// they set.
/// <para>
/// Differences from upstream: the pages and endpoints are the real <c>index</c>, <c>food.json</c>, <c>context/[param]</c>,
/// <c>binary</c> and <c>custom-status</c> routes of the <c>app-pages</c> fixture, whose configuration has
/// <c>site: 'https://mysite.dev/subsite/'</c>. The API context test cannot hand <c>clientAddress</c> to <c>render()</c>: the host
/// gives the address of the connection, so it expects <c>127.0.0.1</c> (the host listens on the loopback interface) instead of
/// the <c>0.0.0.0</c> that upstream passed, and the URL carries the address of the test server instead of <c>example.com</c>.
/// The multipart body is a real <c>multipart/form-data</c> request with a four-byte file, written by hand in the format of a
/// browser (quoted <c>name</c> and <c>filename</c>), which is what <c>FormData</c> of Node writes.
/// </para>
/// <para>
/// Gaps: the two tests that read the status text of an endpoint response (<c>tasty</c>, <c>not ok</c>) fail because the host
/// does not carry the text to the client (GAP-UNITS-2). They keep every assertion of upstream and are skipped. The status
/// code and the body of the same responses are covered by <c>can load a JSON API route, apart from its status text</c>
/// (an extra test, not in upstream) and by <c>can POST to API routes</c>. A second extra test sends the same file as
/// HttpClient's <c>MultipartFormDataContent</c> writes it, with <c>name=file</c> unquoted: Node reads it, Jint's strict
/// multipart parser (the WHATWG algorithm, which needs <c>name="</c>) throws a <c>TypeError</c> and the endpoint answers 500
/// (GAP-UNITS-3). It is skipped; a fix belongs in Jint.WebApi's parser, or in the host by normalizing the part headers.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/app/ssr-api-route.test.ts")]
public class SsrApiRouteTests(SiteHost sites)
{
    private const string Fixture = "app-pages";

    private static readonly (string Name, string Value) PlainText = ("Content-Type", "text/plain;charset=UTF-8");

    private const string Boundary = "----AstroJintBoundary7MA4YWxkTrZu0gW";

    // The body that a browser (or undici's FormData) writes for a form with one file field.
    private static byte[] MultipartFile(byte[] content) =>
    [
        .. Encoding.ASCII.GetBytes($"--{Boundary}\r\nContent-Disposition: form-data; name=\"file\"; filename=\"test.bin\"\r\nContent-Type: application/octet-stream\r\n\r\n"),
        .. content,
        .. Encoding.ASCII.GetBytes($"\r\n--{Boundary}--\r\n"),
    ];

    [Fact(DisplayName = "basic pages work")]
    public async Task Page_IsRendered()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/");

        Assert.NotEqual("", response.Text);
    }

    [Fact(
        DisplayName = "can load a JSON API route",
        Skip = "GAP-UNITS-2: the host does not carry Response.statusText of the endpoint to the client, so it gets the standard phrase OK instead of tasty")]
    public async Task JsonRoute_KeepsItsStatusText()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/food.json");

        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("tasty", response.ReasonPhrase);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(3, body.RootElement.GetArrayLength());
    }

    [Fact(DisplayName = "can load a JSON API route, apart from its status text")]
    public async Task JsonRoute_AnswersWithTheItems()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/food.json");

        Assert.Equal(200, response.StatusCode);
        using var body = JsonDocument.Parse(response.Text);
        Assert.Equal(3, body.RootElement.GetArrayLength());
    }

    [Fact(DisplayName = "has valid API context")]
    public async Task Endpoint_GetsAValidApiContext()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/context/any");

        Assert.Equal(200, response.StatusCode);
        using var json = JsonDocument.Parse(response.Text);
        var data = json.RootElement;
        Assert.True(data.GetProperty("cookiesExist").GetBoolean());
        Assert.True(data.GetProperty("requestExist").GetBoolean());
        Assert.True(data.GetProperty("redirectExist").GetBoolean());
        Assert.True(data.GetProperty("propsExist").GetBoolean());
        var parameter = Assert.Single(data.GetProperty("params").EnumerateObject());
        Assert.Equal("param", parameter.Name);
        Assert.Equal("any", parameter.Value.GetString());
        Assert.Matches("^Astro v", data.GetProperty("generator").GetString());
        Assert.Equal(new Uri(site.BaseAddress, "/context/any").ToString(), data.GetProperty("url").GetString());
        Assert.Equal("127.0.0.1", data.GetProperty("clientAddress").GetString());
        Assert.Equal("https://mysite.dev/subsite/", data.GetProperty("site").GetString());
    }

    [Fact(DisplayName = "can POST to API routes")]
    public async Task Endpoint_AcceptsAPost()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/food.json", "POST", Encoding.UTF8.GetBytes("some data"), PlainText);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("ok", response.Text);
    }

    [Fact(
        DisplayName = "can read custom status text from API routes",
        Skip = "GAP-UNITS-2: the host does not carry Response.statusText of the endpoint to the client, so it gets the standard phrase Bad Request instead of not ok")]
    public async Task Endpoint_KeepsACustomStatusText()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.SendAsync(
            HttpMethod.Post, "/food.json", new StringContent("not some data", Encoding.UTF8, "text/plain"));

        Assert.Equal(400, (int)response.StatusCode);
        Assert.Equal("not ok", response.ReasonPhrase);
        Assert.Equal("not ok", await response.Content.ReadAsStringAsync());
    }

    [Fact(DisplayName = "can be passed binary data from multipart formdata")]
    public async Task Endpoint_ReadsABinaryFileFromAMultipartBody()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync(
            "/binary", "POST", MultipartFile([1, 2, 3, 4]), ("Content-Type", $"multipart/form-data; boundary={Boundary}"));

        Assert.Equal(200, response.StatusCode);
    }

    [Fact(
        DisplayName = "can be passed binary data from multipart formdata, sent by HttpClient",
        Skip = "GAP-UNITS-3: Jint's Request.formData() only accepts quoted Content-Disposition parameters (name=\"file\"); HttpClient's MultipartFormDataContent writes name=file, so the endpoint answers 500 (TypeError: Failed to parse body as multipart/form-data) where Node reads the file")]
    public async Task Endpoint_ReadsABinaryFileFromAMultipartBodyWrittenByHttpClient()
    {
        var site = await sites.GetAsync(Fixture);
        var file = new ByteArrayContent([1, 2, 3, 4]);
        file.Headers.ContentType = new("application/octet-stream");
        using var form = new MultipartFormDataContent { { file, "file", "test.bin" } };

        using var response = await site.SendAsync(HttpMethod.Post, "/binary", form);

        Assert.Equal(200, (int)response.StatusCode);
    }

    [Fact(DisplayName = "custom status: returns a custom status code and empty body for HEAD")]
    public async Task CustomStatus_AnswersAHeadWithoutABody()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/custom-status", "HEAD");

        Assert.Equal(403, response.StatusCode);
        Assert.Equal("", response.Text);
    }

    [Fact(DisplayName = "custom status: returns a 403 status code with the correct body for GET")]
    public async Task CustomStatus_AnswersAGetWithItsBody()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/custom-status");

        Assert.Equal(403, response.StatusCode);
        Assert.Equal("hello world", response.Text);
    }

    [Fact(DisplayName = "custom status: returns the correct headers for GET")]
    public async Task CustomStatus_KeepsItsHeadersForAGet()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/custom-status");

        Assert.Equal("world", response.Header("x-hello"));
    }

    [Fact(DisplayName = "custom status: returns the correct headers for HEAD")]
    public async Task CustomStatus_KeepsItsHeadersForAHead()
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync("/custom-status", "HEAD");

        Assert.Equal("world", response.Header("x-hello"));
    }
}
