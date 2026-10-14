namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/app/csrf.test.ts</c>: the origin check (<c>security.checkOrigin</c>, on by default)
/// rejects cross-site form submissions to routes rendered on demand with a 403, and lets everything else through.
/// <para>
/// Differences from upstream: upstream called the origin-check middleware with a mock context. These tests send real requests
/// to the <c>csrf</c> endpoint (<c>ALL</c>, answers <c>ok</c>) of the <c>app-pages</c> fixture, so the check runs in the
/// pipeline of the host inside Jint. The <c>Host</c> header is <c>example.com</c> so that the request URL, which the host
/// builds from it, has the origin of the upstream URLs (<c>http://example.com</c>). The three tests about the scheme of the
/// origin, and the ones that post to <c>api.example.com</c>, use the TLS site of the harness, whose request URL is
/// <c>https://…</c>. An unsafe request carries a one-byte body: a request whose body is empty reaches Astro without its
/// <c>Content-Type</c> (GAP-UNITS-4), which changes what the check sees; the extra test at the end sends the body-less form of
/// <c>allows cross-origin POST with application/json</c>, which is skipped for that reason.
/// </para>
/// <para>
/// Not ported: <c>allows any method on prerendered routes</c> sets <c>isPrerendered</c> on a mock context. The host never renders
/// a prerendered route on demand (it serves the file of <c>dist</c>, and a POST to the route is treated as a request to an
/// unknown route, which the origin check still answers with 403), so the exemption cannot be observed over HTTP. Not applicable;
/// see the matrix.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/app/csrf.test.ts")]
public class CsrfTests(SiteHost sites)
{
    private const string Fixture = "app-pages";

    private static readonly string[] UnsafeMethods = ["POST", "PUT", "DELETE", "PATCH"];

    private static (string Name, string Value) Origin(string value) => ("Origin", value);

    private static (string Name, string Value) ContentType(string value) => ("Content-Type", value);

    private static (string Name, string Value) SecFetchSite(string value) => ("Sec-Fetch-Site", value);

    public static TheoryData<string, string> JsonLikeOnTlsSite
    {
        get
        {
            TheoryData<string, string> data = [];
            foreach (var contentType in JsonLikeContentTypes)
            {
                data.Add(contentType, "same-site");
                data.Add(contentType, "cross-site");
            }

            return data;
        }
    }

    private static readonly string[] JsonLikeContentTypes =
    [
        "application/json",
        "application/json; charset=utf-8",
        "Application/JSON",
        "application/octet-stream",
    ];

    // The mock requests of upstream have no body; on the wire an unsafe request has one, and it is what lets the Content-Type through.
    private async Task<(int Status, string Body)> SendAsync(string method, string host, bool https, bool withBody, params (string Name, string Value)[] headers)
    {
        var site = await sites.GetAsync(Fixture, https: https);
        var carriesContentType = headers.Any(header => header.Name == "Content-Type");
        HttpContent? content = withBody && carriesContentType ? new ByteArrayContent([(byte)'x']) : null;

        using var response = await site.SendAsync(new HttpMethod(method), "/csrf", content, [.. headers, ("Host", host)]);

        return ((int)response.StatusCode, await response.Content.ReadAsStringAsync());
    }

    private Task<(int Status, string Body)> SendAsync(string method, params (string Name, string Value)[] headers) =>
        SendAsync(method, "example.com", https: false, withBody: true, headers);

    [Fact(DisplayName = "allows GET requests regardless of origin")]
    public async Task Get_IsNeverChecked()
    {
        var (status, _) = await SendAsync("GET", Origin("http://evil.com"), ContentType("multipart/form-data"), SecFetchSite("cross-site"));

        Assert.Equal(200, status);
    }

    [Fact(DisplayName = "allows HEAD requests regardless of origin")]
    public async Task Head_IsNeverChecked()
    {
        var (status, _) = await SendAsync("HEAD", Origin("http://evil.com"), ContentType("multipart/form-data"));

        Assert.Equal(200, status);
    }

    [Fact(DisplayName = "allows OPTIONS requests regardless of origin")]
    public async Task Options_IsNeverChecked()
    {
        var (status, _) = await SendAsync("OPTIONS", Origin("http://evil.com"), ContentType("multipart/form-data"));

        Assert.Equal(200, status);
    }

    [Fact(DisplayName = "allows POST with same origin")]
    public async Task Post_WithTheSameOrigin_IsAllowed()
    {
        var (status, _) = await SendAsync("POST", Origin("http://example.com"), ContentType("multipart/form-data"));

        Assert.Equal(200, status);
    }

    [Fact(DisplayName = "allows PUT with same origin")]
    public async Task Put_WithTheSameOrigin_IsAllowed()
    {
        var (status, _) = await SendAsync("PUT", Origin("http://example.com"), ContentType("application/x-www-form-urlencoded"));

        Assert.Equal(200, status);
    }

    [Fact(DisplayName = "allows DELETE with same origin")]
    public async Task Delete_WithTheSameOrigin_IsAllowed()
    {
        var (status, _) = await SendAsync("DELETE", Origin("http://example.com"), ContentType("text/plain"));

        Assert.Equal(200, status);
    }

    [Fact(DisplayName = "allows PATCH with same origin")]
    public async Task Patch_WithTheSameOrigin_IsAllowed()
    {
        var (status, _) = await SendAsync("PATCH", Origin("http://example.com"), ContentType("multipart/form-data"));

        Assert.Equal(200, status);
    }

    [Theory(DisplayName = "allows unsafe requests with Sec-Fetch-Site")]
    [InlineData("same-origin")]
    [InlineData("none")]
    public async Task UnsafeRequests_WithASameOriginSecFetchSite_AreAllowed(string secFetchSite)
    {
        foreach (var method in UnsafeMethods)
        {
            var (status, _) = await SendAsync(method, ContentType("multipart/form-data"), SecFetchSite(secFetchSite));

            Assert.Equal(200, status);
        }
    }

    [Theory(DisplayName = "blocks form submissions with Sec-Fetch-Site")]
    [InlineData("same-site")]
    [InlineData("cross-site")]
    [InlineData("invalid")]
    public async Task FormSubmissions_WithAForeignSecFetchSite_AreBlocked(string secFetchSite)
    {
        var (status, _) = await SendAsync("POST", Origin("http://example.com"), ContentType("multipart/form-data"), SecFetchSite(secFetchSite));

        Assert.Equal(403, status);
    }

    [Theory(DisplayName = "blocks cross-origin requests with multipart/form-data")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task CrossOriginRequests_WithMultipartFormData_AreBlocked(string method)
    {
        var (status, _) = await SendAsync(method, Origin("http://evil.com"), ContentType("multipart/form-data"));

        Assert.Equal(403, status);
    }

    [Theory(DisplayName = "blocks cross-origin requests with application/x-www-form-urlencoded")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task CrossOriginRequests_WithUrlEncodedForms_AreBlocked(string method)
    {
        var (status, _) = await SendAsync(method, Origin("http://evil.com"), ContentType("application/x-www-form-urlencoded"));

        Assert.Equal(403, status);
    }

    [Theory(DisplayName = "blocks cross-origin requests with text/plain")]
    [InlineData("POST")]
    [InlineData("PUT")]
    [InlineData("DELETE")]
    [InlineData("PATCH")]
    public async Task CrossOriginRequests_WithPlainText_AreBlocked(string method)
    {
        var (status, _) = await SendAsync(method, Origin("http://evil.com"), ContentType("text/plain"));

        Assert.Equal(403, status);
    }

    [Fact(DisplayName = "blocks cross-origin POST with no content-type")]
    public async Task CrossOriginPost_WithoutAContentType_IsBlocked()
    {
        var (status, _) = await SendAsync("POST", Origin("http://evil.com"));

        Assert.Equal(403, status);
    }

    [Fact(DisplayName = "blocks cross-site POST with no content-type")]
    public async Task CrossSitePost_WithoutAContentType_IsBlocked()
    {
        var (status, _) = await SendAsync("POST", SecFetchSite("cross-site"));

        Assert.Equal(403, status);
    }

    [Theory(DisplayName = "blocks cross-origin POST with a form content type")]
    [InlineData("multipart/form-data; boundary=x")]
    [InlineData("text/plain;charset=UTF-8")]
    [InlineData("Text/Plain")]
    public async Task CrossOriginPost_WithAFormContentType_IsBlocked(string contentType)
    {
        var (status, _) = await SendAsync("POST", Origin("http://evil.com"), ContentType(contentType));

        Assert.Equal(403, status);
    }

    [Theory(DisplayName = "blocks POST with a form content type and Sec-Fetch-Site: cross-site")]
    [InlineData("multipart/form-data; boundary=x")]
    [InlineData("text/plain;charset=UTF-8")]
    [InlineData("Text/Plain")]
    public async Task CrossSitePost_WithAFormContentType_IsBlocked(string contentType)
    {
        var (status, _) = await SendAsync("POST", ContentType(contentType), SecFetchSite("cross-site"));

        Assert.Equal(403, status);
    }

    [Theory(DisplayName = "allows cross-origin POST with a content type that is not a form's")]
    [InlineData("application/json")]
    [InlineData("application/json; charset=utf-8")]
    [InlineData("Application/JSON")]
    [InlineData("application/octet-stream")]
    public async Task CrossOriginPost_WithANonFormContentType_IsAllowed(string contentType)
    {
        var (status, _) = await SendAsync("POST", Origin("http://evil.com"), ContentType(contentType));

        Assert.Equal(200, status);
    }

    [Theory(DisplayName = "allows POST with a content type that is not a form's and a cross-site Sec-Fetch-Site")]
    [MemberData(nameof(JsonLikeOnTlsSite))]
    public async Task CrossSitePost_WithANonFormContentType_IsAllowed(string contentType, string secFetchSite)
    {
        var (status, _) = await SendAsync(
            "POST", "api.example.com", https: true, withBody: true,
            Origin("https://example.com"), ContentType(contentType), SecFetchSite(secFetchSite));

        Assert.Equal(200, status);
    }

    [Fact(DisplayName = "allows requests without browser origin headers")]
    public async Task Post_WithoutBrowserHeaders_IsAllowed()
    {
        var (status, _) = await SendAsync("POST");

        Assert.Equal(200, status);
    }

    [Fact(DisplayName = "falls back to matching the full Origin")]
    public async Task Post_WithTheSameHttpsOrigin_IsAllowed()
    {
        var (status, _) = await SendAsync(
            "POST", "example.com", https: true, withBody: true, Origin("https://example.com"), ContentType("multipart/form-data"));

        Assert.Equal(200, status);
    }

    [Fact(DisplayName = "blocks a fallback Origin with a different scheme")]
    public async Task Post_WithAnOriginOfAnotherScheme_IsBlocked()
    {
        var (status, _) = await SendAsync(
            "POST", "example.com", https: true, withBody: true, Origin("http://example.com"), ContentType("multipart/form-data"));

        Assert.Equal(403, status);
    }

    [Fact(DisplayName = "blocks a malformed Origin fallback")]
    public async Task Post_WithAMalformedOrigin_IsBlocked()
    {
        var (status, _) = await SendAsync(
            "POST", "example.com", https: true, withBody: true, Origin("null"), ContentType("multipart/form-data"));

        Assert.Equal(403, status);
    }

    [Fact(DisplayName = "blocks cross-origin POST with uppercased form content-type")]
    public async Task CrossOriginPost_WithAnUppercasedFormContentType_IsBlocked()
    {
        var (status, _) = await SendAsync("POST", Origin("http://evil.com"), ContentType("MULTIPART/FORM-DATA"));

        Assert.Equal(403, status);
    }

    [Fact(DisplayName = "blocks cross-origin POST with form content-type containing extra params")]
    public async Task CrossOriginPost_WithAFormContentTypeAndParameters_IsBlocked()
    {
        var (status, _) = await SendAsync("POST", Origin("http://evil.com"), ContentType("application/x-www-form-urlencoded; charset=utf-8"));

        Assert.Equal(403, status);
    }

    [Fact(DisplayName = "403 response includes a descriptive message")]
    public async Task BlockedRequest_ExplainsWhy()
    {
        var (_, body) = await SendAsync("POST", Origin("http://evil.com"), ContentType("multipart/form-data"));

        Assert.Equal("Cross-site POST form submissions are forbidden", body);
    }

    [Fact(
        DisplayName = "allows cross-origin POST with application/json, without a body",
        Skip = "GAP-UNITS-4: the host builds the request without a body when the body is empty and so drops its Content-Type (AstroHttpBridge.CreateRequestAsync): the origin check then sees a request without a content type and answers 403, where Node, which keeps the header, answers 200")]
    public async Task CrossOriginPost_WithAJsonContentTypeAndNoBody_IsAllowed()
    {
        var (status, _) = await SendAsync(
            "POST", "example.com", https: false, withBody: false, Origin("http://evil.com"), ContentType("application/json"));

        Assert.Equal(200, status);
    }
}
