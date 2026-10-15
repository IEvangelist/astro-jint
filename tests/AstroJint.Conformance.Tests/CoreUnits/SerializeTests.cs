using System.Text.Json;
using static AstroJint.Conformance.CoreUnits.AppActions;
using static AstroJint.Conformance.CoreUnits.FormBodies;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/actions/serialize.test.ts</c>: how Astro turns the result of an action into a response
/// (<c>serializeActionResult</c>, devalue for data and JSON for errors, 204 for no data) and back into a result
/// (<c>deserializeActionResult</c>, what <c>Astro.getActionResult()</c> gives a page).
/// <para>
/// Differences from upstream: neither function can be called from a test of the host, so each test is observed through the one
/// that uses it. The <c>serializeActionResult</c> tests post to the RPC route of an action of the <c>app-actions</c> fixture
/// that returns, or throws, what the test needs (<c>serialize</c> takes the kind of result in its JSON input, <c>throwCode</c> the
/// code of the error), and assert the status, the <c>Content-Type</c> and the body of the response; the body of data is the
/// exact devalue text (deterministic), where upstream parsed it back with <c>devalue.parse</c>. The <c>deserializeActionResult</c>
/// tests post a form to the page <c>form-result.astro</c> with <c>?_action=serializeForm</c> (the action of the same results for
/// forms); the page prints what <c>Astro.getActionResult()</c> gave it as JSON. 'handles malformed error body gracefully' has its
/// result made by the caller of <c>App.render()</c>, as upstream did: the host's <c>LocalsFactory</c> gives the page an action
/// payload whose body is not JSON. The input error is the real one of zod (<c>comment</c> missing from the form of the
/// <c>comment</c> action), whose message is not 'Required', so the test compares the field with the message of the issue instead of
/// with a literal. 'throws when data contains a Response object' cannot observe the error type
/// (<c>ActionsReturnedInvalidDataError</c>), only that the request fails with 500.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/actions/serialize.test.ts")]
public class SerializeTests(SiteHost sites)
{
    private const string DevalueType = "application/json+devalue";

    // The RPC call of the action `serialize`, which returns the data (or throws the error) that `kind` names.
    private async Task<RawResponse> SerializeAsync(string kind)
    {
        var site = await sites.GetActionsSiteAsync();
        return await PostJsonAsync(site, "/_actions/serialize", $$"""{"kind":"{{kind}}"}""");
    }

    private async Task AssertDataAsync(string kind, string devalueBody)
    {
        var response = await SerializeAsync(kind);

        Assert.Equal(200, response.StatusCode);
        Assert.Equal(DevalueType, response.Header("Content-Type"));
        Assert.Equal(devalueBody, response.Text);
    }

    // The page of the deserialize tests: the form posted to the action serializeForm, and what the page made of the result.
    private async Task<JsonElement> DeserializeAsync(params Part[] parts)
    {
        var site = await sites.GetActionsSiteAsync();
        return View(await PostFormAsync(site, "/form-result?_action=serializeForm", parts));
    }

    // #region serializeActionResult

    [Fact(DisplayName = "serializes a string")]
    public Task SerializeActionResult_String_IsDevalue() => AssertDataAsync("string", """["hello"]""");

    [Fact(DisplayName = "serializes a number")]
    public Task SerializeActionResult_Number_IsDevalue() => AssertDataAsync("number", "[42]");

    [Fact(DisplayName = "serializes 0")]
    public Task SerializeActionResult_Zero_IsDevalue() => AssertDataAsync("zero", "[0]");

    [Fact(DisplayName = "serializes false")]
    public Task SerializeActionResult_False_IsDevalue() => AssertDataAsync("false", "[false]");

    [Fact(DisplayName = "serializes null")]
    public Task SerializeActionResult_Null_IsDevalue() => AssertDataAsync("null", "[null]");

    [Fact(DisplayName = "serializes an object")]
    public Task SerializeActionResult_Object_IsDevalue() =>
        AssertDataAsync("object", """[{"channel":1,"subscribeButtonState":2},"bholmesdev","smashed"]""");

    [Fact(DisplayName = "serializes complex values: Date, Set, URL")]
    public Task SerializeActionResult_DateSetAndUrl_AreDevalue() => AssertDataAsync(
        "complex",
        """[{"date":1,"set":2,"url":6},["Date","2024-01-01T00:00:00.000Z"],["Set",3,4,5],1,2,3,["URL",7],"https://astro.build/"]""");

    [Fact(DisplayName = "returns type \"empty\" with status 204 when data is undefined")]
    public async Task SerializeActionResult_Undefined_Is204WithoutBody()
    {
        var response = await SerializeAsync("empty");

        Assert.Equal(204, response.StatusCode);
        Assert.Empty(response.Body);
    }

    [Fact(DisplayName = "serializes an ActionError")]
    public async Task SerializeActionResult_ActionError_IsJsonWithItsStatus()
    {
        var response = await SerializeAsync("unauthorized");

        Assert.Equal(401, response.StatusCode);
        Assert.Equal("application/json", response.Header("Content-Type"));
        var body = JsonDocument.Parse(response.Text).RootElement;
        Assert.Equal("AstroActionError", body.GetProperty("type").GetString());
        Assert.Equal("UNAUTHORIZED", body.GetProperty("code").GetString());
        Assert.Equal("Not logged in", body.GetProperty("message").GetString());
    }

    [Fact(DisplayName = "serializes an ActionInputError with issues and fields")]
    public async Task SerializeActionResult_ActionInputError_HasIssuesAndFields()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await PostFormAsync(site, "/_actions/comment", Field("channel", "bholmesdev"));

        Assert.Equal(400, response.StatusCode);
        Assert.Equal("application/json", response.Header("Content-Type"));
        var body = JsonDocument.Parse(response.Text).RootElement;
        Assert.Equal("AstroActionInputError", body.GetProperty("type").GetString());
        var issues = body.GetProperty("issues");
        Assert.Equal(JsonValueKind.Array, issues.ValueKind);
        Assert.Equal("invalid_type", issues[0].GetProperty("code").GetString());
        Assert.Equal("comment", issues[0].GetProperty("path")[0].GetString());
        var message = issues[0].GetProperty("message").GetString();
        Assert.False(string.IsNullOrEmpty(message));
        Assert.Equal(message, body.GetProperty("fields").GetProperty("comment")[0].GetString());
    }

    [Fact(DisplayName = "uses correct status for different error codes")]
    public async Task SerializeActionResult_ErrorCodes_HaveTheirStatus()
    {
        var site = await sites.GetActionsSiteAsync();

        foreach (var (code, expectedStatus) in new[]
        {
            ("BAD_REQUEST", 400),
            ("NOT_FOUND", 404),
            ("INTERNAL_SERVER_ERROR", 500),
            ("CONTENT_TOO_LARGE", 413),
            ("UNSUPPORTED_MEDIA_TYPE", 415),
        })
        {
            var response = await PostJsonAsync(site, "/_actions/throwCode", $$"""{"code":"{{code}}"}""");

            Assert.Equal((code, expectedStatus), (code, response.StatusCode));
        }
    }

    [Fact(DisplayName = "throws when data contains a Response object")]
    public async Task SerializeActionResult_ResponseAsData_Fails()
    {
        var response = await SerializeAsync("response");

        Assert.Equal(500, response.StatusCode);
    }

    // #endregion

    // #region deserializeActionResult

    [Fact(DisplayName = "deserializes a data result")]
    public async Task DeserializeActionResult_Data_IsTheData()
    {
        var view = await DeserializeAsync(Field("kind", "object"));

        Assert.Equal(JsonValueKind.Null, view.GetProperty("error").ValueKind);
        Assert.Equal("bholmesdev", view.GetProperty("channel").GetString());
    }

    [Fact(DisplayName = "deserializes an empty result")]
    public async Task DeserializeActionResult_Empty_HasNoDataAndNoError()
    {
        var view = await DeserializeAsync(Field("kind", "empty"));

        Assert.False(view.GetProperty("hasData").GetBoolean());
        Assert.Equal(JsonValueKind.Null, view.GetProperty("error").ValueKind);
    }

    [Fact(DisplayName = "deserializes an ActionError result")]
    public async Task DeserializeActionResult_ActionError_IsAnActionError()
    {
        var view = await DeserializeAsync(Field("kind", "unauthorized"));

        Assert.False(view.GetProperty("hasData").GetBoolean());
        var error = view.GetProperty("error");
        Assert.True(error.GetProperty("isActionError").GetBoolean());
        Assert.Equal("UNAUTHORIZED", error.GetProperty("code").GetString());
        Assert.Equal("Not logged in", error.GetProperty("message").GetString());
    }

    [Fact(DisplayName = "deserializes an ActionInputError result")]
    public async Task DeserializeActionResult_InputError_IsAnInputErrorWithIssues()
    {
        var view = await DeserializeAsync();

        Assert.False(view.GetProperty("hasData").GetBoolean());
        var error = view.GetProperty("error");
        Assert.True(error.GetProperty("isInputError").GetBoolean());
        Assert.Equal("BAD_REQUEST", error.GetProperty("code").GetString());
        Assert.True(error.GetProperty("hasIssues").GetBoolean());
    }

    [Fact(DisplayName = "roundtrips complex data types")]
    public async Task DeserializeActionResult_DateAndSet_Roundtrip()
    {
        var view = await DeserializeAsync(Field("kind", "roundtrip"));

        Assert.Equal("2024-06-15T00:00:00.000Z", view.GetProperty("date").GetString());
        Assert.Equal(["a", "b"], view.GetProperty("set").EnumerateArray().Select(item => item.GetString()));
    }

    [Fact(DisplayName = "roundtrips 0 and false")]
    public async Task DeserializeActionResult_ZeroAndFalse_Roundtrip()
    {
        var zero = await DeserializeAsync(Field("kind", "zero"));
        var no = await DeserializeAsync(Field("kind", "false"));

        Assert.True(zero.GetProperty("hasData").GetBoolean());
        Assert.Equal(0, zero.GetProperty("data").GetInt32());
        Assert.True(no.GetProperty("hasData").GetBoolean());
        Assert.False(no.GetProperty("data").GetBoolean());
    }

    [Fact(DisplayName = "handles malformed error body gracefully")]
    public async Task DeserializeActionResult_MalformedErrorBody_IsAnInternalServerError()
    {
        var site = await sites.GetActionResultSiteAsync();

        var response = await site.SendRawAsync("/form-result?_action=serializeForm", headers: ("x-action-result", "malformed-error"));

        var error = View(response).GetProperty("error");
        Assert.True(error.GetProperty("isActionError").GetBoolean());
        Assert.Equal("INTERNAL_SERVER_ERROR", error.GetProperty("code").GetString());
        Assert.Equal("not valid json", error.GetProperty("message").GetString());
    }

    // #endregion
}
