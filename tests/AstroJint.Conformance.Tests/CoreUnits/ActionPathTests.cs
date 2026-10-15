using static AstroJint.Conformance.CoreUnits.AppActions;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/actions/action-path.test.ts</c>: the query string of an action
/// (<c>?_action=&lt;name&gt;</c>) and the path of its RPC route (<c>getActionPath()</c>: the base, <c>/_actions/&lt;name&gt;</c>, the
/// trailing slash), inside Jint.
/// <para>
/// Differences from upstream: <c>getActionQueryString</c> and <c>getActionPathFromString</c> are not exported by
/// <c>astro:actions</c>, so the endpoint <c>src/pages/action-api.json.ts</c> of the <c>app-actions</c> fixture reads the query string
/// from the <c>actions</c> proxy (<c>actions.x.toString()</c>, which calls <c>getActionQueryString</c>) and the path from
/// <c>getActionPath(actions.x)</c> (which calls <c>getActionPathFromString</c> with the base and the trailing slash of the build).
/// Upstream passed the base and the trailing slash as arguments; here they are the configuration of the site: Astro appends the
/// slash for <c>trailingSlash: 'always'</c> and, with <c>'ignore'</c>, for the build format <c>'directory'</c> (the default), so the
/// default site and the site with a base give a path that ends with a slash, where upstream passed
/// <c>shouldAppendTrailingSlash: false</c> ('returns the correct path for a simple action', 'includes base path' and 'handles nested
/// action names' are adapted to that). 'strips trailing slash from base before building path' runs on a site with
/// <c>base: '/base/'</c> and <c>build.format: 'file'</c> (the only configuration whose <c>BASE_URL</c> keeps a trailing slash
/// while the path gets none; <c>trailingSlash: 'never'</c> would have removed the slash of the base). The proxy encodes the name once
/// itself (<c>encodeURIComponent</c>) before the query string encodes it again, so the special characters of 'encodes special
/// characters' show as <c>with%252Fslash</c> in the query string (the name <c>with/slash</c> reaches the proxy as one key).
/// </para>
/// </summary>
[Trait("Upstream", "core-units/actions/action-path.test.ts")]
public class ActionPathTests(SiteHost sites)
{
    private async Task<System.Text.Json.JsonElement> ProxyAsync(string keys) =>
        await ProbeAsync(await sites.GetActionsSiteAsync(), "proxy", ("keys", keys));

    // #region getActionQueryString

    [Fact(DisplayName = "encodes action name as _action query param")]
    public async Task QueryString_Action_IsTheActionParameter()
    {
        var action = await ProxyAsync("subscribe");

        Assert.Equal("?_action=subscribe", action.GetProperty("toString").GetString());
    }

    [Fact(DisplayName = "encodes nested action paths")]
    public async Task QueryString_NestedAction_IsTheDottedPath()
    {
        var action = await ProxyAsync("user,admins,auth");

        Assert.Equal("?_action=user.admins.auth", action.GetProperty("toString").GetString());
    }

    [Fact(DisplayName = "encodes special characters")]
    public async Task QueryString_SpecialCharacters_AreEncoded()
    {
        var action = await ProxyAsync("with/slash");

        Assert.Equal("?_action=with%252Fslash", action.GetProperty("toString").GetString());
    }

    // #endregion

    // #region getActionPathFromString

    [Fact(DisplayName = "returns the correct path for a simple action")]
    public async Task Path_SimpleAction_IsTheRpcRoute()
    {
        var action = await ProxyAsync("subscribe");

        Assert.Equal("/_actions/subscribe/", action.GetProperty("path").GetString());
    }

    [Fact(DisplayName = "includes base path")]
    public async Task Path_WithABase_StartsWithTheBase()
    {
        var site = await sites.GetBaseSiteAsync();

        var action = await ProbeAtAsync(site, "/base", "proxy", ("keys", "subscribe"));

        Assert.Equal("/base/_actions/subscribe/", action.GetProperty("path").GetString());
    }

    [Fact(DisplayName = "strips trailing slash from base before building path")]
    public async Task Path_BaseWithATrailingSlash_HasNoDoubleSlash()
    {
        var site = await sites.GetFileFormatSiteAsync();

        var action = await ProbeAtAsync(site, "/base", "proxy", ("keys", "subscribe"));

        Assert.Equal("/base/_actions/subscribe", action.GetProperty("path").GetString());
    }

    [Fact(DisplayName = "appends trailing slash when configured")]
    public async Task Path_WithTrailingSlashAlways_EndsWithASlash()
    {
        var site = await sites.GetTrailingSlashSiteAsync();

        var action = await ProbeAsync(site, "proxy", ("keys", "subscribe"));

        Assert.Equal("/_actions/subscribe/", action.GetProperty("path").GetString());
    }

    [Fact(DisplayName = "combines base path with trailing slash")]
    public async Task Path_WithABaseAndATrailingSlash_HasBoth()
    {
        var site = await sites.GetBaseSiteAsync();

        var action = await ProbeAtAsync(site, "/base", "proxy", ("keys", "transformFormInput"));

        Assert.Equal("/base/_actions/transformFormInput/", action.GetProperty("path").GetString());
    }

    [Fact(DisplayName = "handles nested action names")]
    public async Task Path_NestedAction_IsTheDottedPath()
    {
        var action = await ProxyAsync("user,admins,auth");

        Assert.Equal("/_actions/user.admins.auth/", action.GetProperty("path").GetString());
    }

    // #endregion
}
