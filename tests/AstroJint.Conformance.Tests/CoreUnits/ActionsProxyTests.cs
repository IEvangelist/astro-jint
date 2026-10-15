using System.Text.Json;
using static AstroJint.Conformance.CoreUnits.AppActions;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/actions/actions-proxy.test.ts</c>: the proxy <c>actions</c> that builds the path of an
/// action from the properties that are read (<c>actions.user.admins.auth</c>), gives its query string, its progressive
/// enhancement data and <c>orThrow</c>, inside Jint.
/// <para>
/// Differences from upstream: <c>createActionsProxy</c> is not exported and the tests gave it a recording <c>handleAction</c>. The
/// tests here use the proxy that the server entry of <c>astro:actions</c> builds, in the endpoint
/// <c>src/pages/action-api.json.ts</c> of the <c>app-actions</c> fixture, with Astro's real <c>handleAction</c>. The path that was built
/// is observed in the query string of the action (<c>toString()</c>: <c>?_action=&lt;path&gt;</c>, in which the path is encoded
/// once by the proxy and again by <c>URLSearchParams</c>), not as the argument of the handler (so the parameter that is forwarded
/// to it is not observed). The two <c>orThrow</c> tests call the real actions <c>subscribe</c> (data) and <c>getUserOrThrow</c> (an
/// <c>UNAUTHORIZED</c> error) through <c>context.callAction()</c>, which Astro requires on the server.
/// </para>
/// <para>
/// Not ported (N/A): 'returns symbol properties from the target' and 'returns own properties from the target' pass an
/// <c>actionCallback</c> to <c>createActionsProxy</c>, an argument that only the client entry's own use of the function sets.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/actions/actions-proxy.test.ts")]
public class ActionsProxyTests(SiteHost sites)
{
    private async Task<JsonElement> ProxyAsync(string keys) =>
        await ProbeAsync(await sites.GetActionsSiteAsync(), "proxy", ("keys", keys));

    // #region path building

    [Fact(DisplayName = "builds a top-level path from property access")]
    public async Task Path_TopLevelProperty_IsTheName()
    {
        var action = await ProxyAsync("subscribe");

        Assert.Equal("?_action=subscribe", action.GetProperty("toString").GetString());
    }

    [Fact(DisplayName = "builds a nested path with dot separators")]
    public async Task Path_NestedProperties_AreJoinedWithDots()
    {
        var action = await ProxyAsync("user,admins,auth");

        Assert.Equal("?_action=user.admins.auth", action.GetProperty("toString").GetString());
    }

    [Fact(DisplayName = "encodes dots in property names as %2E")]
    public async Task Path_DotInAName_IsEncoded()
    {
        var action = await ProxyAsync("my.action");

        Assert.Equal("?_action=my%252Eaction", action.GetProperty("toString").GetString());
    }

    [Fact(DisplayName = "encodes special characters in property names")]
    public async Task Path_SpecialCharacters_AreEncoded()
    {
        var action = await ProxyAsync("with/slash");

        Assert.Equal("?_action=with%252Fslash", action.GetProperty("toString").GetString());
    }

    // #endregion

    // #region toString / queryString

    [Fact(DisplayName = "toString returns the action query string")]
    public async Task ToString_IsTheQueryString()
    {
        var action = await ProxyAsync("subscribe");

        Assert.Equal("?_action=subscribe", action.GetProperty("toString").GetString());
    }

    [Fact(DisplayName = "queryString matches toString")]
    public async Task QueryString_MatchesToString()
    {
        var action = await ProxyAsync("subscribe");

        Assert.Equal(action.GetProperty("toString").GetString(), action.GetProperty("queryString").GetString());
    }

    [Fact(DisplayName = "nested action toString includes full path")]
    public async Task ToString_NestedAction_IncludesTheFullPath()
    {
        var action = await ProxyAsync("user,auth");

        Assert.Equal("?_action=user.auth", action.GetProperty("toString").GetString());
    }

    // #endregion

    // #region $$FORM_ACTION

    [Fact(DisplayName = "returns progressive enhancement info for React")]
    public async Task FormAction_HasMethodNameAndAction()
    {
        var action = await ProxyAsync("subscribe");

        var formAction = action.GetProperty("formAction");
        Assert.Equal("POST", formAction.GetProperty("method").GetString());
        Assert.Equal("_astroAction", formAction.GetProperty("name").GetString());
        Assert.Contains("_action=subscribe", formAction.GetProperty("action").GetString());
    }

    // #endregion

    // #region orThrow

    [Fact(DisplayName = "returns data on success")]
    public async Task OrThrow_OnSuccess_ReturnsTheData()
    {
        var site = await sites.GetActionsSiteAsync();

        var result = await ProbeAsync(site, "orThrow", ("name", "subscribe"));

        Assert.False(result.TryGetProperty("threw", out _));
        Assert.Equal("test", result.GetProperty("data").GetProperty("channel").GetString());
    }

    [Fact(DisplayName = "throws on error")]
    public async Task OrThrow_OnError_ThrowsTheActionError()
    {
        var site = await sites.GetActionsSiteAsync();

        var result = await ProbeAsync(site, "orThrow", ("name", "getUserOrThrow"));

        Assert.True(result.GetProperty("threw").GetBoolean());
        Assert.True(result.GetProperty("isActionError").GetBoolean());
        Assert.Equal("UNAUTHORIZED", result.GetProperty("code").GetString());
    }

    // #endregion
}
