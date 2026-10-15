using System.Text;
using static AstroJint.Conformance.CoreUnits.AppServerIslands;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/server-islands/endpoint.test.ts</c>, which calls <c>getRequestData()</c> with
/// <c>Request</c> objects (the parsing of the GET query and of the POST body, the 400, 405 and 413 answers) and renders the endpoint
/// of two hand-made islands through a <c>FetchState</c>. Here the requests go to the real endpoint <c>/_server-islands/Island</c> of
/// the <c>app-server-islands</c> fixture, over HTTP, and the answer is what the test asserts on: the status, its text and, where the
/// unit test only checked that the options were parsed, the island that those options render (the values that the test sends are
/// really encrypted, with the key of the fixture, because the endpoint decrypts them). 11 tests are ported (the 400s of the missing
/// parameters and of the plaintext and malformed bodies, the 413 of a body over the limit, the 405 methods and the two propagated
/// islands, whose fragments are asserted as upstream does); 9 are adapted: the six whose result is the render of the island instead
/// of the <c>RenderOptions</c> (the two well-formed requests, the two with empty values and the two that accept a body within the
/// limit), the one about the <c>Content-Length</c> header (see its comment), and the two that pollute <c>Object.prototype</c>,
/// which no request can do, so that the probe <c>/probe/endpoint</c> runs them with the same <c>getRequestData()</c> inside the
/// host. The limit that the unit tests pass (100 and 10000 bytes) is the 200 bytes of the build of the fixture that has the CSP
/// (<c>security.serverIslandBodySizeLimit</c>); the other build has the default of 1 MiB, which the default-limit test also
/// exercises at its boundary. The two propagated islands are <c>src/components/runtime/*.mjs</c>, which use the same functions of
/// the runtime as the unit tests do.
/// </summary>
[Trait("Upstream", "core-units/server-islands/endpoint.test.ts")]
public class ServerIslandsEndpointTests(SiteHost sites)
{
    private const string Endpoint = "/_server-islands/Island";

    // The props and the slot that the tests encrypt for the island, and what it renders for them.
    private const string Props = """{"message":"hello"}""";

    private const string Slots = """{"default":"<p>slot</p>"}""";

    private static async Task AssertMissingParameterAsync(ConformanceSite site, string target)
    {
        using var response = await site.SendAsync(HttpMethod.Get, target);

        Assert.Equal(400, (int)response.StatusCode);
        Assert.Equal("Bad request: Missing required query parameters.", response.ReasonPhrase);
    }

    private static async Task AssertRenderedAsync(HttpResponseMessage response, string expectedProps, string? expectedSlot = null)
    {
        Assert.Equal(200, (int)response.StatusCode);
        Assert.Equal("noindex", Assert.Single(response.HeaderValues("X-Robots-Tag")));

        var island = ConformanceSite.ParseHtml(await response.Content.ReadAsStringAsync()).QuerySelector("#island");
        Assert.Equal(expectedProps, island?.GetAttribute("data-props"));
        if (expectedSlot is not null)
        {
            Assert.Equal(expectedSlot, island?.QuerySelector("p")?.TextContent);
        }
    }

    private static string EncryptedExportBody(string padding = "") =>
        $$"""{"encryptedComponentExport":"{{EncryptedExport("Island")}}","encryptedProps":"","encryptedSlots":""}{{padding}}""";

    private async Task AssertParsedByTheFunctionAsync(string caseName)
    {
        var probe = await (await sites.GetServerIslandsSiteAsync()).ProbeAsync("endpoint");

        var result = probe[caseName];

        // `getRequestData()` returned the options (what the probe answers as `options`), not a Response (which it answers as a status).
        if (result.TryGetProperty("status", out var status))
        {
            Assert.Fail($"Expected the options but the function answered {status} {result.Str("statusText")}");
        }

        var options = result.GetProperty("options");
        Assert.Equal("encExport", options.Str("encryptedComponentExport"));
        Assert.Equal("encProps", options.Str("encryptedProps"));
        Assert.Equal("encSlots", options.Str("encryptedSlots"));
    }

    private async Task<string> FetchIslandOfAsync(string page)
    {
        var site = await sites.GetServerIslandsSiteAsync();
        var response = await site.SendRawAsync(page);
        Assert.Equal(200, response.StatusCode);

        var island = Assert.Single(Islands(response.Text));
        using var fragment = await island.FetchAsync(site);
        Assert.Equal(200, (int)fragment.StatusCode);
        return await fragment.Content.ReadAsStringAsync();
    }

    // #region GET requests

    [Fact(DisplayName = "returns RenderOptions when all required params are present")]
    public async Task Get_RendersTheIslandWhenAllTheParametersArePresent()
    {
        var site = await sites.GetServerIslandsSiteAsync();

        // The three values as the page of the island sends them, encrypted: the endpoint renders what they say.
        using var response = await site.SendAsync(HttpMethod.Get, IslandTarget("Island", Props, Slots));

        await AssertRenderedAsync(response, Props, "slot");
    }

    [Fact(DisplayName = "returns 400 when `s` is missing")]
    public async Task Get_Answers400WhenTheSlotsAreMissing()
    {
        var site = await sites.GetServerIslandsSiteAsync();

        await AssertMissingParameterAsync(site, $"{Endpoint}?e=export&p=props");
    }

    [Fact(DisplayName = "returns 400 when `e` is missing")]
    public async Task Get_Answers400WhenTheExportIsMissing()
    {
        var site = await sites.GetServerIslandsSiteAsync();

        await AssertMissingParameterAsync(site, $"{Endpoint}?s=slots&p=props");
    }

    [Fact(DisplayName = "returns 400 when `p` is missing")]
    public async Task Get_Answers400WhenThePropsAreMissing()
    {
        var site = await sites.GetServerIslandsSiteAsync();

        await AssertMissingParameterAsync(site, $"{Endpoint}?s=slots&e=export");
    }

    [Fact(DisplayName = "returns 400 when all params are missing")]
    public async Task Get_Answers400WhenAllTheParametersAreMissing()
    {
        var site = await sites.GetServerIslandsSiteAsync();

        await AssertMissingParameterAsync(site, Endpoint);
    }

    [Fact(DisplayName = "accepts empty-string param values (empty props / slots are valid)")]
    public async Task Get_AcceptsEmptyPropsAndSlots()
    {
        var site = await sites.GetServerIslandsSiteAsync();

        // `p` and `s` are present and empty: the island has no props and no slots.
        using var response = await site.SendAsync(HttpMethod.Get, $"{Endpoint}?{Query(EncryptedExport("Island"), "", "")}");

        await AssertRenderedAsync(response, "{}");
    }

    // #endregion

    // #region POST requests

    [Fact(DisplayName = "returns RenderOptions for a well-formed encrypted payload")]
    public async Task Post_RendersTheIslandForAWellFormedPayload()
    {
        var site = await sites.GetServerIslandsSiteAsync();

        using var response = await site.SendAsync(HttpMethod.Post, Endpoint, IslandBody("Island", Props, Slots));

        await AssertRenderedAsync(response, Props, "slot");
    }

    [Fact(DisplayName = "returns 400 when POST body contains plaintext `slots` object")]
    public async Task Post_Answers400WhenTheBodyHasPlaintextSlots()
    {
        var site = await sites.GetServerIslandsSiteAsync();

        using var response = await site.SendAsync(
            HttpMethod.Post,
            Endpoint,
            JsonBody(new { encryptedComponentExport = "encExport", encryptedProps = "", slots = new { @default = "<p>Hello</p>" } }));

        Assert.Equal(400, (int)response.StatusCode);
        Assert.Contains("plaintext slots", response.ReasonPhrase, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "returns 400 when POST body contains plaintext `componentExport` string")]
    public async Task Post_Answers400WhenTheBodyHasAPlaintextComponentExport()
    {
        var site = await sites.GetServerIslandsSiteAsync();

        using var response = await site.SendAsync(
            HttpMethod.Post,
            Endpoint,
            JsonBody(new { componentExport = "default", encryptedProps = "", encryptedSlots = "" }));

        Assert.Equal(400, (int)response.StatusCode);
        Assert.Contains("plaintext componentexport", response.ReasonPhrase, StringComparison.OrdinalIgnoreCase);
    }

    [Fact(DisplayName = "returns 400 when POST body is malformed JSON")]
    public async Task Post_Answers400WhenTheBodyIsMalformedJson()
    {
        var site = await sites.GetServerIslandsSiteAsync();

        using var response = await site.SendAsync(
            HttpMethod.Post,
            Endpoint,
            new StringContent("not valid json {{{", Encoding.UTF8, "application/json"));

        Assert.Equal(400, (int)response.StatusCode);
        Assert.Equal("Bad request: Request format is invalid.", response.ReasonPhrase);
    }

    [Fact(DisplayName = "accepts empty strings for encryptedProps and encryptedSlots")]
    public async Task Post_AcceptsEmptyStringsForThePropsAndTheSlots()
    {
        var site = await sites.GetServerIslandsSiteAsync();

        using var response = await site.SendAsync(HttpMethod.Post, Endpoint, IslandBody("Island"));

        await AssertRenderedAsync(response, "{}");
    }

    [Fact(DisplayName = "only checks own properties for `slots` validation")]
    public async Task Post_OnlyChecksOwnPropertiesForTheSlots() =>
        await AssertParsedByTheFunctionAsync("only checks own properties for `slots` validation");

    [Fact(DisplayName = "only checks own properties for `componentExport` validation")]
    public async Task Post_OnlyChecksOwnPropertiesForTheComponentExport() =>
        await AssertParsedByTheFunctionAsync("only checks own properties for `componentExport` validation");

    // #endregion

    // #region POST body size limiting

    [Fact(DisplayName = "returns 413 when POST body exceeds the configured limit")]
    public async Task PostBodySize_Answers413WhenTheBodyExceedsTheLimit()
    {
        // The build with the CSP limits the body to 200 bytes (the unit test passes 100): this body has 270.
        var site = await sites.GetServerIslandsCspSiteAsync();

        using var response = await site.SendAsync(
            HttpMethod.Post,
            Endpoint,
            JsonBody(new { encryptedComponentExport = new string('x', 200), encryptedProps = "", encryptedSlots = "" }));

        Assert.Equal(413, (int)response.StatusCode);
        Assert.Equal($"Request body exceeds the configured limit of {BodyLimit} bytes", response.ReasonPhrase);
    }

    [Fact(DisplayName = "returns 413 when Content-Length header exceeds the configured limit")]
    public async Task PostBodySize_Answers413WhenTheContentLengthExceedsTheLimit()
    {
        var site = await sites.GetServerIslandsCspSiteAsync();

        // The unit test builds a request with `Content-Length: 999999` and a body of 70 bytes. A request over HTTP/1.1 cannot say that: the
        // server would wait for the missing bytes (or would refuse the request), and the host hands the script a request without the
        // header, which counts the bytes of the body as they arrive. So this request is one that really is larger than the limit, which
        // is what the header of the unit test announces: it gets the same answer.
        using var response = await site.SendAsync(
            HttpMethod.Post,
            Endpoint,
            JsonBody(new { encryptedComponentExport = new string('x', 10_000), encryptedProps = "", encryptedSlots = "" }));

        Assert.Equal(413, (int)response.StatusCode);
    }

    [Fact(DisplayName = "accepts POST body within the configured limit")]
    public async Task PostBodySize_AcceptsABodyWithinTheLimit()
    {
        var site = await sites.GetServerIslandsCspSiteAsync();

        // A valid body is a bit over 120 bytes, within the 200 of the limit (the unit test has 10 KB for a body of 80 bytes).
        using var within = await site.SendAsync(HttpMethod.Post, Endpoint, IslandBody("Island"));
        await AssertRenderedAsync(within, "{}");

        // The limit is a limit of bytes: a body of exactly that size passes (the JSON has trailing spaces) and one byte more does not.
        var body = EncryptedExportBody();
        Assert.True(body.Length < BodyLimit);
        using var exact = await site.SendAsync(HttpMethod.Post, Endpoint, new StringContent(body.PadRight(BodyLimit), Encoding.UTF8, "application/json"));
        await AssertRenderedAsync(exact, "{}");

        using var over = await site.SendAsync(HttpMethod.Post, Endpoint, new StringContent(body.PadRight(BodyLimit + 1), Encoding.UTF8, "application/json"));
        Assert.Equal(413, (int)over.StatusCode);
    }

    [Fact(DisplayName = "uses default limit when no limit is specified")]
    public async Task PostBodySize_UsesTheDefaultLimitWhenThereIsNone()
    {
        // The default build does not set the limit: a body of the size of any island passes ...
        var site = await sites.GetServerIslandsSiteAsync();

        using var accepted = await site.SendAsync(HttpMethod.Post, Endpoint, IslandBody("Island", Props, Slots));
        await AssertRenderedAsync(accepted, Props, "slot");

        // ... and the default is 1 MiB: a body over it is refused.
        using var refused = await site.SendAsync(
            HttpMethod.Post,
            Endpoint,
            JsonBody(new { encryptedComponentExport = new string('x', 1024 * 1024), encryptedProps = "", encryptedSlots = "" }));
        Assert.Equal(413, (int)refused.StatusCode);
    }

    // #endregion

    // #region Unsupported HTTP methods

    [Theory(DisplayName = "returns 405 for")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    [InlineData("HEAD")]
    public async Task Methods_Answer405(string method)
    {
        var site = await sites.GetServerIslandsSiteAsync();

        var response = await site.SendRawAsync(Endpoint, method);

        Assert.Equal(405, response.StatusCode);
    }

    // #endregion

    // #region server island endpoint

    [Fact(DisplayName = "serializes propagated head assets into the fragment response")]
    public async Task Fragment_SerializesThePropagatedHeadAssets()
    {
        // The island of the page /propagated renders a component that has head content (a style, a stylesheet, a module script).
        var fragment = await FetchIslandOfAsync("/propagated");

        var document = ConformanceSite.ParseHtml(fragment);
        Assert.Single(document.QuerySelectorAll("style"));
        Assert.Equal(".box{color:red}", document.QuerySelector("style")?.TextContent);
        Assert.Single(document.QuerySelectorAll("link[rel=stylesheet][href='/content.css']"));
        Assert.Single(document.QuerySelectorAll("script[type=module][src='/content.js']"));
        Assert.Equal("Island content", document.QuerySelector(".box")?.TextContent);
        // The head content comes first in the fragment.
        Assert.StartsWith("<style>", fragment, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "waits for async slots before serializing propagated head assets")]
    public async Task Fragment_WaitsForAsyncSlotsBeforeSerializingTheHeadAssets()
    {
        // The island of the page /propagated-async has a wrapper whose slot resolves after 10 ms to a component with head content.
        var fragment = await FetchIslandOfAsync("/propagated-async");

        var document = ConformanceSite.ParseHtml(fragment);
        Assert.Equal(".async{color:red}", document.QuerySelector("style")?.TextContent);
        Assert.Equal("Async content", document.QuerySelector(".async")?.TextContent);
        Assert.StartsWith("<style>", fragment, StringComparison.Ordinal);
    }

    // #endregion
}
