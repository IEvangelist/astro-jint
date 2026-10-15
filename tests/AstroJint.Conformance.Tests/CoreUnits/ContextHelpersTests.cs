using System.Text.Json;
using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/render/context-helpers.test.ts</c>: the getters <c>Astro.session</c> and <c>Astro.csp</c>
/// give <c>undefined</c> when the manifest has no session driver or no CSP, and <c>Astro.csp</c> gives an object with the five
/// <c>insert*</c> methods when it has one. Upstream renders a page of its own through a test <c>App</c> whose manifest it
/// patches (<c>csp: { ... }</c>); the page <c>/context-helpers</c> of the <c>app-render</c> fixture prints what the two getters
/// give, and the builds differ in the config that sets the manifest.
/// <para>
/// The tests are adapted for the manifest: the adapter turns sessions on by default (the driver is the distributed cache of the
/// host), so 'no session driver' is a build with <c>session: false</c> and 'CSP configured' a build with
/// <c>security: { csp: true }</c>, where upstream patched the manifest object. One build with both serves the first and the third
/// test, and the default build (sessions on, no CSP) serves the second. The first two tests also check the warning that Astro
/// writes through the logger when a getter is read without its configuration.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/render/context-helpers.test.ts")]
public class ContextHelpersTests(SiteHost sites)
{
    private static readonly object NoSessionsWithCsp = new { session = false, security = new { csp = true } };

    private static async Task<JsonElement> ViewAsync(ConformanceSite site)
    {
        var response = await site.SendRawAsync("/context-helpers");
        Assert.Equal(200, response.StatusCode);
        return JsonDocument.Parse(AppRouting.Text(response, "#view")).RootElement;
    }

    [Fact(DisplayName = "returns undefined when no session driver is configured")]
    public async Task Session_IsUndefinedWithoutADriver()
    {
        var site = await sites.GetAsync(Fixture, NoSessionsWithCsp);

        var view = await ViewAsync(site);

        Assert.Equal("undefined", view.Str("session"));
        Assert.Contains(
            site.ConsoleEntries(),
            entry => entry.Message.Contains("`Astro.session` was accessed but no session storage is configured", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "returns undefined when CSP is not configured in the manifest")]
    public async Task Csp_IsUndefinedWhenNotConfigured()
    {
        var site = await sites.GetAsync(Fixture);

        var view = await ViewAsync(site);

        Assert.Equal("undefined", view.Str("csp"));
        Assert.Equal("object", view.Str("session"));
        Assert.Contains(
            site.ConsoleEntries(),
            entry => entry.Message.Contains("context.csp was used when rendering the route", StringComparison.Ordinal));
    }

    [Fact(DisplayName = "returns an object with insert* methods when CSP is configured")]
    public async Task Csp_HasTheInsertMethodsWhenConfigured()
    {
        var site = await sites.GetAsync(Fixture, NoSessionsWithCsp);

        var view = await ViewAsync(site);

        Assert.Equal("object", view.Str("csp"));
        Assert.Equal("function", view.Str("insertScriptHash"));
        Assert.Equal("function", view.Str("insertStyleHash"));
        Assert.Equal("function", view.Str("insertScriptResource"));
        Assert.Equal("function", view.Str("insertStyleResource"));
        Assert.Equal("function", view.Str("insertDirective"));
    }
}
