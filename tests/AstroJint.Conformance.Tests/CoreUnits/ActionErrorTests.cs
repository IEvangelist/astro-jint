using System.Text.Json;
using static AstroJint.Conformance.CoreUnits.AppActions;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/actions/action-error.test.ts</c>: the client classes and helpers of the actions
/// runtime (<c>ActionError</c> and its codes, <c>ActionInputError</c>, <c>isActionError</c>, <c>isInputError</c>) behave the same
/// inside Jint.
/// <para>
/// Differences from upstream: the tests import the module of the build, which a test of the host cannot do, so the helpers run in
/// the endpoint <c>src/pages/action-api.json.ts</c> of the <c>app-actions</c> fixture, which imports them from
/// <c>astro:actions</c> and answers with what they give as JSON (<c>GET /action-api.json?probe=...</c>). <c>ActionInputError</c>
/// and <c>codeToStatusMap</c> are not exported there: the input errors are built by <c>ActionError.fromJson()</c>, which calls the
/// constructor with the issues (so <c>instanceof ActionInputError</c> is observed as <c>isInputError()</c> plus the
/// <c>type</c>), and the table of codes is written down in this class (copied from <c>codeToStatusMap</c>). <c>assert.ok(error
/// instanceof ActionError)</c> is observed by the endpoint, in the engine, as a Boolean.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/actions/action-error.test.ts")]
public class ActionErrorTests(SiteHost sites)
{
    private static readonly (string Code, int Status)[] CodeToStatus =
    [
        ("BAD_REQUEST", 400), ("UNAUTHORIZED", 401), ("PAYMENT_REQUIRED", 402), ("FORBIDDEN", 403), ("NOT_FOUND", 404),
        ("METHOD_NOT_ALLOWED", 405), ("NOT_ACCEPTABLE", 406), ("PROXY_AUTHENTICATION_REQUIRED", 407), ("REQUEST_TIMEOUT", 408),
        ("CONFLICT", 409), ("GONE", 410), ("LENGTH_REQUIRED", 411), ("PRECONDITION_FAILED", 412), ("CONTENT_TOO_LARGE", 413),
        ("URI_TOO_LONG", 414), ("UNSUPPORTED_MEDIA_TYPE", 415), ("RANGE_NOT_SATISFIABLE", 416), ("EXPECTATION_FAILED", 417),
        ("MISDIRECTED_REQUEST", 421), ("UNPROCESSABLE_CONTENT", 422), ("LOCKED", 423), ("FAILED_DEPENDENCY", 424),
        ("TOO_EARLY", 425), ("UPGRADE_REQUIRED", 426), ("PRECONDITION_REQUIRED", 428), ("TOO_MANY_REQUESTS", 429),
        ("REQUEST_HEADER_FIELDS_TOO_LARGE", 431), ("UNAVAILABLE_FOR_LEGAL_REASONS", 451), ("INTERNAL_SERVER_ERROR", 500),
        ("NOT_IMPLEMENTED", 501), ("BAD_GATEWAY", 502), ("SERVICE_UNAVAILABLE", 503), ("GATEWAY_TIMEOUT", 504),
        ("HTTP_VERSION_NOT_SUPPORTED", 505), ("VARIANT_ALSO_NEGOTIATES", 506), ("INSUFFICIENT_STORAGE", 507),
        ("LOOP_DETECTED", 508), ("NETWORK_AUTHENTICATION_REQUIRED", 511),
    ];

    private async Task<JsonElement> ProbeAsync(string probe, params (string Name, string Value)[] args) =>
        await AppActions.ProbeAsync(await sites.GetActionsSiteAsync(), probe, args);

    // The kinds of values that `is` of the endpoint checks: isActionError and isInputError of each.
    private async Task<(bool IsActionError, bool IsInputError)> IsAsync(string value)
    {
        var result = await ProbeAsync("is", ("value", value));
        return (result.GetProperty("isActionError").GetBoolean(), result.GetProperty("isInputError").GetBoolean());
    }

    // #region ActionError

    [Fact(DisplayName = "sets code, status, and message from constructor")]
    public async Task Constructor_SetsCodeStatusMessageAndType()
    {
        var error = await ProbeAsync("construct", ("code", "NOT_FOUND"), ("message", "Page not found"));

        Assert.Equal("NOT_FOUND", error.GetProperty("code").GetString());
        Assert.Equal(404, error.GetProperty("status").GetInt32());
        Assert.Equal("Page not found", error.GetProperty("message").GetString());
        Assert.Equal("AstroActionError", error.GetProperty("type").GetString());
    }

    [Fact(DisplayName = "defaults message to empty string when omitted")]
    public async Task Constructor_WithoutMessage_HasAnEmptyMessage()
    {
        var error = await ProbeAsync("construct", ("code", "INTERNAL_SERVER_ERROR"));

        Assert.Equal("INTERNAL_SERVER_ERROR", error.GetProperty("code").GetString());
        Assert.Equal(500, error.GetProperty("status").GetInt32());
        Assert.Equal("", error.GetProperty("message").GetString());
    }

    [Fact(DisplayName = "preserves stack when provided")]
    public async Task Constructor_WithAStack_KeepsIt()
    {
        var error = await ProbeAsync("construct", ("code", "BAD_REQUEST"), ("message", "bad"), ("stack", "custom stack"));

        Assert.Equal("custom stack", error.GetProperty("stack").GetString());
    }

    [Fact(DisplayName = "is an instance of Error")]
    public async Task ActionError_IsAnError()
    {
        var error = await ProbeAsync("construct", ("code", "BAD_REQUEST"));

        Assert.True(error.GetProperty("isError").GetBoolean());
    }

    // #endregion

    // #region ActionError.codeToStatus

    [Fact(DisplayName = "maps all known codes to correct HTTP status")]
    public async Task CodeToStatus_MapsEveryCode()
    {
        var statuses = await ProbeAsync("codeToStatus", ("codes", string.Join(",", CodeToStatus.Select(entry => entry.Code))));

        foreach (var (code, status) in CodeToStatus)
        {
            Assert.Equal((code, status), (code, statuses.GetProperty(code).GetInt32()));
        }
    }

    // #endregion

    // #region ActionError.statusToCode

    [Fact(DisplayName = "maps known statuses back to codes")]
    public async Task StatusToCode_MapsKnownStatuses()
    {
        var codes = await ProbeAsync("statusToCode", ("statuses", "400,401,404,500"));

        Assert.Equal("BAD_REQUEST", codes.GetProperty("400").GetString());
        Assert.Equal("UNAUTHORIZED", codes.GetProperty("401").GetString());
        Assert.Equal("NOT_FOUND", codes.GetProperty("404").GetString());
        Assert.Equal("INTERNAL_SERVER_ERROR", codes.GetProperty("500").GetString());
    }

    [Fact(DisplayName = "falls back to INTERNAL_SERVER_ERROR for unknown statuses")]
    public async Task StatusToCode_UnknownStatus_IsInternalServerError()
    {
        var codes = await ProbeAsync("statusToCode", ("statuses", "999"));

        Assert.Equal("INTERNAL_SERVER_ERROR", codes.GetProperty("999").GetString());
    }

    // #endregion

    // #region ActionError.fromJson

    [Fact(DisplayName = "reconstructs an ActionError from serialized JSON")]
    public async Task FromJson_ActionError_IsAnActionError()
    {
        var error = await ProbeAsync(
            "fromJson", ("json", """{"type":"AstroActionError","code":"UNAUTHORIZED","message":"Not logged in"}"""));

        Assert.True(error.GetProperty("isActionError").GetBoolean());
        Assert.Equal("UNAUTHORIZED", error.GetProperty("code").GetString());
        Assert.Equal("Not logged in", error.GetProperty("message").GetString());
    }

    [Fact(DisplayName = "reconstructs an ActionInputError from serialized JSON")]
    public async Task FromJson_InputError_IsAnInputErrorWithIssues()
    {
        var error = await ProbeAsync(
            "fromJson",
            ("json", """{"type":"AstroActionInputError","issues":[{"code":"invalid_type","message":"Required","path":["name"]}]}"""));

        Assert.True(error.GetProperty("isActionError").GetBoolean());
        Assert.True(error.GetProperty("isInputError").GetBoolean());
        Assert.Equal("AstroActionInputError", error.GetProperty("type").GetString());
        Assert.Equal(JsonValueKind.Array, error.GetProperty("issues").ValueKind);
    }

    [Fact(DisplayName = "returns a generic ActionError for unrecognized shapes")]
    public async Task FromJson_UnknownShape_IsAnInternalServerError()
    {
        var error = await ProbeAsync("fromJson", ("json", """{"something":"random"}"""));

        Assert.True(error.GetProperty("isActionError").GetBoolean());
        Assert.Equal("INTERNAL_SERVER_ERROR", error.GetProperty("code").GetString());
    }

    // #endregion

    // #region ActionInputError

    [Fact(DisplayName = "sets code to BAD_REQUEST and populates fields from issues")]
    public async Task InputError_HasCodeStatusAndFields()
    {
        var error = await ProbeAsync(
            "fromJson",
            (
                "json",
                """
                {"type":"AstroActionInputError","issues":[
                  {"code":"invalid_type","message":"Expected string","path":["name"]},
                  {"code":"too_small","message":"Too short","path":["name"]},
                  {"code":"invalid_type","message":"Required","path":["email"]}]}
                """));

        Assert.Equal("BAD_REQUEST", error.GetProperty("code").GetString());
        Assert.Equal(400, error.GetProperty("status").GetInt32());
        Assert.Equal("AstroActionInputError", error.GetProperty("type").GetString());
        var fields = error.GetProperty("fields");
        Assert.Equal(["Expected string", "Too short"], fields.GetProperty("name").EnumerateArray().Select(item => item.GetString()));
        Assert.Equal(["Required"], fields.GetProperty("email").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact(DisplayName = "is an instance of ActionError")]
    public async Task InputError_IsAnActionErrorAndAnError()
    {
        var error = await ProbeAsync("fromJson", ("json", """{"type":"AstroActionInputError","issues":[]}"""));

        Assert.True(error.GetProperty("isActionError").GetBoolean());
        Assert.True(error.GetProperty("isError").GetBoolean());
    }

    [Fact(DisplayName = "handles issues without paths")]
    public async Task InputError_IssuesWithoutPaths_HaveNoFields()
    {
        var error = await ProbeAsync(
            "fromJson",
            ("json", """{"type":"AstroActionInputError","issues":[{"code":"custom","message":"Something wrong","path":[]}]}"""));

        Assert.Empty(error.GetProperty("fields").EnumerateObject());
    }

    // #endregion

    // #region isActionError

    [Fact(DisplayName = "returns true for ActionError instances")]
    public async Task IsActionError_ActionError_IsTrue() => Assert.True((await IsAsync("action-error")).IsActionError);

    [Fact(DisplayName = "returns false for ActionInputError instances (different type field)")]
    public async Task IsActionError_InputError_IsFalse() => Assert.False((await IsAsync("input-error")).IsActionError);

    [Fact(DisplayName = "returns true for plain objects with type AstroActionError")]
    public async Task IsActionError_PlainObject_IsTrue() => Assert.True((await IsAsync("plain-action-error")).IsActionError);

    [Fact(DisplayName = "returns false for regular errors")]
    public async Task IsActionError_RegularError_IsFalse() => Assert.False((await IsAsync("error")).IsActionError);

    [Fact(DisplayName = "returns false for null/undefined")]
    public async Task IsActionError_NullAndUndefined_IsFalse()
    {
        Assert.False((await IsAsync("null")).IsActionError);
        Assert.False((await IsAsync("undefined")).IsActionError);
    }

    [Fact(DisplayName = "returns false for strings")]
    public async Task IsActionError_String_IsFalse() => Assert.False((await IsAsync("string")).IsActionError);

    // #endregion

    // #region isInputError

    [Fact(DisplayName = "returns true for ActionInputError instances")]
    public async Task IsInputError_InputError_IsTrue() => Assert.True((await IsAsync("input-error-with-issue")).IsInputError);

    [Fact(DisplayName = "returns true for plain objects matching the shape")]
    public async Task IsInputError_PlainObject_IsTrue() => Assert.True((await IsAsync("plain-input-error")).IsInputError);

    [Fact(DisplayName = "returns false for ActionError (non-input)")]
    public async Task IsInputError_ActionError_IsFalse() => Assert.False((await IsAsync("action-error")).IsInputError);

    [Fact(DisplayName = "returns false for objects without issues array")]
    public async Task IsInputError_WithoutIssues_IsFalse() =>
        Assert.False((await IsAsync("plain-input-error-without-issues")).IsInputError);

    // #endregion
}
