using System.Text;
using System.Text.Json;
using static AstroJint.Conformance.CoreUnits.AppActions;
using static AstroJint.Conformance.CoreUnits.FormBodies;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/actions/actions-app.test.ts</c>: Astro's action runtime behind <c>App.render()</c>. The
/// RPC route <c>/_actions/[...path]</c> runs JSON and form actions and answers with devalue, errors and the statuses of the
/// action errors; the form schemas of <c>formDataToObject</c> and zod (nested objects, discriminated unions, effects, files)
/// are applied to multipart bodies; a middleware guards RPC requests; the base path and the trailing slash apply to the route;
/// and malformed requests are answered with 400 and 404.
/// <para>
/// Differences from upstream: <c>createActionsApp()</c> gave <c>createTestApp()</c> an action table and two inline routes. Here
/// the table is <c>src/actions/index.ts</c> of the <c>app-actions</c> fixture, Astro injects the RPC route itself, <c>/test</c> is the
/// real empty page <c>test.astro</c> and the guard is <c>src/middleware.ts</c>. The options of the manifest are the Astro
/// configuration of three more builds of the fixture: <c>base: '/base'</c>, <c>trailingSlash: 'always'</c> and
/// <c>security.actionBodySizeLimit: 100</c> (the other tests use the default of 1 MiB); the origin check is off for all of them,
/// as the requests have no <c>Origin</c> header. <c>devalue.parse(body)</c> is replaced by comparing the body with the exact
/// devalue text, which is deterministic. The forms are real <c>multipart/form-data</c> bodies written by hand, in the format of
/// undici's <c>FormData</c> (an empty form is the closing boundary alone), and the file of a form is an empty part named
/// like upstream's <c>new File([''], name)</c>. <c>Content-Length: 0</c> is sent by the harness for an empty body.
/// <c>does not resolve properties of an action function as a path</c> asks the host for the action afterwards and expects the
/// counter to be 1, on a host with a single engine so that both requests meet the same module state (upstream read the closure
/// variable <c>calls</c>); the <c>greet</c> action of 'malformed' is the table of the fixture too. The tests that upstream runs
/// on different <c>RouteData</c> objects (<c>respects base path</c>, <c>supports trailing slash</c>) run on the sites of the
/// matching builds.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/actions/actions-app.test.ts")]
public class ActionsAppTests(SiteHost sites)
{
    // The body of an action error is JSON, and has its type, code and message.
    private static JsonElement ErrorBody(RawResponse response) => JsonDocument.Parse(response.Text).RootElement;

    // #region Actions via App

    [Fact(DisplayName = "exposes a JSON action (subscribe)")]
    public async Task JsonAction_RunsAndReturnsDevalue()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await PostJsonAsync(site, "/_actions/subscribe", """{"channel":"bholmesdev"}""");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/json+devalue", response.Header("Content-Type"));
        Assert.Equal("""[{"channel":1,"subscribeButtonState":2},"bholmesdev","smashed"]""", response.Text);
    }

    [Fact(DisplayName = "exposes a form action (comment)")]
    public async Task FormAction_RunsAndReturnsDevalue()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await PostFormAsync(site, "/_actions/comment", Field("channel", "bholmesdev"), Field("comment", "Hello, World!"));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/json+devalue", response.Header("Content-Type"));
        Assert.Equal("""[{"channel":1,"comment":2},"bholmesdev","Hello, World!"]""", response.Text);
    }

    [Fact(DisplayName = "exposes plain formData action")]
    public async Task PlainFormDataAction_ReceivesTheFormData()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await PostFormAsync(site, "/_actions/commentPlainFormData", Field("anything", "works"));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("""[{"success":1,"isFormData":1},true]""", response.Text);
    }

    [Fact(DisplayName = "returns 404 for nonexistent action")]
    public async Task UnknownAction_Is404()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await PostJsonAsync(site, "/_actions/doesNotExist", "{}");

        Assert.Equal(404, response.StatusCode);
    }

    [Fact(DisplayName = "returns 404 for prototype methods used as action names")]
    public async Task PrototypeMethodName_Is404()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await PostJsonAsync(site, "/_actions/toString", "{}");

        Assert.Equal(404, response.StatusCode);
    }

    [Fact(DisplayName = "does not resolve properties of an action function as a path")]
    public async Task ActionFunctionProperty_IsNotResolvedAsAPath()
    {
        var site = await sites.GetSingleEngineSiteAsync();

        var orThrow = await PostJsonAsync(site, "/_actions/increment.orThrow", "{}");
        var increment = await PostJsonAsync(site, "/_actions/increment", "{}");

        Assert.Equal(404, orThrow.StatusCode);
        Assert.Equal("""[{"calls":1},1]""", increment.Text);
    }

    [Fact(DisplayName = "returns 404 for GET requests")]
    public async Task GetRequest_Is404()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await site.SendRawAsync("/_actions/subscribe");

        Assert.Equal(404, response.StatusCode);
    }

    [Fact(DisplayName = "returns 204 for void action (fire and forget)")]
    public async Task VoidAction_Is204()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await site.SendRawAsync("/_actions/fireAndForget", "POST", [], JsonType);

        Assert.Equal(204, response.StatusCode);
    }

    [Fact(DisplayName = "respects custom errors")]
    public async Task ThrownActionError_KeepsItsStatusCodeAndMessage()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await PostFormAsync(site, "/_actions/getUserOrThrow");

        Assert.Equal(401, response.StatusCode);
        var body = ErrorBody(response);
        Assert.Equal("AstroActionError", body.GetProperty("type").GetString());
        Assert.Equal("UNAUTHORIZED", body.GetProperty("code").GetString());
        Assert.Equal("Not logged in", body.GetProperty("message").GetString());
    }

    [Fact(DisplayName = "raises validation error on bad form data")]
    public async Task MissingFormField_IsAnInputError()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await PostFormAsync(site, "/_actions/comment", Field("channel", "bholmesdev"));

        Assert.Equal(400, response.StatusCode);
        var body = ErrorBody(response);
        Assert.Equal("AstroActionInputError", body.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Array, body.GetProperty("issues").ValueKind);
    }

    [Fact(DisplayName = "handles special characters in action names (space)")]
    public async Task ActionName_WithAnEncodedSpace_IsDecoded()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await PostJsonAsync(site, "/_actions/with%20space", """{"name":"Ben"}""");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("""["Hello, Ben!"]""", response.Text);
    }

    [Fact(DisplayName = "handles special characters in action names (slash)")]
    public async Task ActionName_WithAnEncodedSlash_IsDecoded()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await PostJsonAsync(site, "/_actions/with%2Fslash", """{"name":"Ben"}""");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("""["Hello, Ben!"]""", response.Text);
    }

    // #endregion

    // #region Actions body size limit

    [Fact(DisplayName = "rejects oversized JSON body")]
    public async Task OversizedJsonBody_Is413()
    {
        var site = await sites.GetSmallLimitSiteAsync();

        var response = await PostJsonAsync(site, "/_actions/subscribe", $$"""{"channel":"{{new string('x', 200)}}"}""");

        Assert.Equal(413, response.StatusCode);
        Assert.Equal("CONTENT_TOO_LARGE", ErrorBody(response).GetProperty("code").GetString());
    }

    [Fact(DisplayName = "returns 204 when content-length is 0")]
    public async Task EmptyJsonBody_Is204()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await site.SendRawAsync("/_actions/fireAndForget", "POST", [], JsonType);

        Assert.Equal(204, response.StatusCode);
    }

    [Fact(DisplayName = "returns 204 when content-type is omitted")]
    public async Task MissingContentType_Is204()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await site.SendRawAsync("/_actions/fireAndForget", "POST", []);

        Assert.Equal(204, response.StatusCode);
    }

    [Fact(DisplayName = "returns 415 when content-type is unexpected")]
    public async Task UnexpectedContentType_Is415()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await site.SendRawAsync(
            "/_actions/subscribe", "POST", Encoding.UTF8.GetBytes("not json"), ("Content-Type", "text/plain"));

        Assert.Equal(415, response.StatusCode);
    }

    // #endregion

    // #region Form schema handling (nested objects, discriminated unions, refine, transform)

    [Fact(DisplayName = "handles nested form objects with dot notation")]
    public async Task NestedFormObject_IsBuiltFromDottedNames()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await PostFormAsync(
            site, "/_actions/nestedFormObject", Field("a", "hello"), Field("bc.b", "hoge"), Field("bc.c", "world"));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/json+devalue", response.Header("Content-Type"));
        Assert.Equal("""[{"a":1,"bc":2},"hello",{"b":3,"c":4},"hoge","world"]""", response.Text);
    }

    [Fact(DisplayName = "validates nested form objects with superRefine")]
    public async Task NestedFormObject_RunsItsSuperRefine()
    {
        var site = await sites.GetActionsSiteAsync();

        // bc.c is omitted to trigger the superRefine validation
        var response = await PostFormAsync(site, "/_actions/nestedFormObject", Field("a", "hello"), Field("bc.b", "huga"));

        Assert.Equal(400, response.StatusCode);
        var body = ErrorBody(response);
        Assert.Equal("AstroActionInputError", body.GetProperty("type").GetString());
        Assert.Contains(
            body.GetProperty("issues").EnumerateArray(),
            issue => issue.GetProperty("path").EnumerateArray().Any(segment => segment.GetString() == "c"));
    }

    [Fact(DisplayName = "handles nested discriminatedUnion in form data")]
    public async Task NestedDiscriminatedUnion_IsBuiltFromDottedNames()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await PostFormAsync(
            site, "/_actions/nestedDiscriminatedUnion",
            Field("name", "Ben"), Field("contact.type", "email"), Field("contact.email", "ben@test.test"));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/json+devalue", response.Header("Content-Type"));
        Assert.Equal("""[{"name":1,"contact":2},"Ben",{"type":3,"email":4},"email","ben@test.test"]""", response.Text);
    }

    [Fact(DisplayName = "supports effects on form input validators")]
    public async Task RefinedFormInput_ReportsItsIssue()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await PostFormAsync(
            site, "/_actions/validatePassword", Field("password", "benisawesome"), Field("confirmPassword", "benisveryawesome"));

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("application/json", response.Header("Content-Type"));
        var body = ErrorBody(response);
        Assert.Equal("AstroActionInputError", body.GetProperty("type").GetString());
        Assert.Equal("Passwords do not match", body.GetProperty("issues")[0].GetProperty("message").GetString());
    }

    [Fact(DisplayName = "supports complex chained effects on form input validators")]
    public async Task ChainedFormInputEffects_AreApplied()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await PostFormAsync(
            site, "/_actions/validatePasswordComplex",
            Field("currentPassword", "benisboring"), Field("newPassword", "benisawesome"), Field("confirmNewPassword", "benisawesome"));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/json+devalue", response.Header("Content-Type"));
        // exactly the two keys of the pipe, in this order
        Assert.Equal("""[{"currentPassword":1,"newPassword":2},"benisboring","benisawesome"]""", response.Text);
    }

    [Fact(DisplayName = "supports input form data transforms")]
    public async Task FormDataTransform_ReceivesTheFormData()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await PostFormAsync(site, "/_actions/transformFormInput", Field("name", "ben"), Field("age", "42"));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/json+devalue", response.Header("Content-Type"));
        Assert.Equal("""[{"name":1,"age":2},"ben","42"]""", response.Text);
    }

    [Fact(DisplayName = "supports discriminated union for different form fields")]
    public async Task DiscriminatedUnion_AcceptsEachVariantOfTheForm()
    {
        var site = await sites.GetActionsSiteAsync();

        var first = await PostFormAsync(
            site, "/_actions/imageUploadInChunks", Field("type", "first-chunk"), Field("alt", "Cool image"), Upload("image", "chunk-1.png"));
        var rest = await PostFormAsync(
            site, "/_actions/imageUploadInChunks", Field("type", "rest-chunk"), Field("uploadId", "fake"), Upload("image", "chunk-2.png"));

        Assert.Equal(200, first.StatusCode);
        Assert.Equal("application/json+devalue", first.Header("Content-Type"));
        Assert.False(string.IsNullOrEmpty(JsonDocument.Parse(first.Text).RootElement[1].GetString()));
        Assert.Equal(200, rest.StatusCode);
        Assert.Equal("""[{"uploadId":1},"fake"]""", rest.Text);
    }

    // #endregion

    // #region Middleware integration

    [Fact(DisplayName = "blocks RPC when middleware guard rejects")]
    public async Task RpcRequest_IsBlockedByTheMiddleware()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await PostJsonAsync(site, "/_actions/locked", "{}");

        Assert.Equal(401, response.StatusCode);
    }

    [Fact(DisplayName = "allows RPC when middleware guard passes")]
    public async Task RpcRequest_PassesTheMiddleware()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await site.SendRawAsync(
            "/_actions/locked", "POST", Encoding.UTF8.GetBytes("{}"), JsonType, ("Cookie", "actionCookie=1234"));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/json+devalue", response.Header("Content-Type"));
        Assert.Equal("""[{"safe":1},true]""", response.Text);
    }

    // #endregion

    // #region Base path and trailing slash

    [Fact(DisplayName = "respects base path")]
    public async Task Rpc_IsServedUnderTheBasePath()
    {
        var site = await sites.GetBaseSiteAsync();

        var response = await PostFormAsync(
            site, "/base/_actions/comment", Field("channel", "bholmesdev"), Field("comment", "Hello, World!"));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/json+devalue", response.Header("Content-Type"));
        Assert.Equal("""[{"channel":1,"comment":2},"bholmesdev","Hello, World!"]""", response.Text);
    }

    [Fact(DisplayName = "supports trailing slash")]
    public async Task Rpc_IsServedWithATrailingSlash()
    {
        var site = await sites.GetTrailingSlashSiteAsync();

        var response = await PostFormAsync(
            site, "/_actions/comment/", Field("channel", "bholmesdev"), Field("comment", "Hello, World!"));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("application/json+devalue", response.Header("Content-Type"));
        Assert.Equal("""[{"channel":1,"comment":2},"bholmesdev","Hello, World!"]""", response.Text);
    }

    // #endregion

    // #region Malformed request handling

    [Fact(DisplayName = "returns 400 for malformed JSON body")]
    public async Task MalformedJsonBody_Is400()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await PostJsonAsync(site, "/_actions/greet", "a=b");

        Assert.Equal(400, response.StatusCode);
        var body = ErrorBody(response);
        Assert.Equal("BAD_REQUEST", body.GetProperty("code").GetString());
        Assert.Equal("Invalid JSON request body", body.GetProperty("message").GetString());
    }

    [Fact(DisplayName = "returns 404 for undecodable action name")]
    public async Task UndecodableActionName_Is404()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await PostJsonAsync(site, "/_actions/%25", "{}");

        Assert.Equal(404, response.StatusCode);
    }

    [Fact(DisplayName = "returns 404 for undecodable form action name")]
    public async Task UndecodableFormActionName_Is404()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await PostFormAsync(site, "/test?_action=%25", Field("name", "Astro"));

        Assert.Equal(404, response.StatusCode);
    }

    // #endregion
}
