using System.Net;
using System.Text.RegularExpressions;

namespace AstroJint.Conformance.CoreBuild;

/// <summary>
/// Port of <c>packages/astro/test/container.test.ts</c>: endpoints that render components with <c>astro/container</c> when they
/// are requested, with the React and Vue renderers added to the container, a component that has a client directive and an Astro
/// component imported with <c>?container</c> (its stylesheet and scripts come out around the markup). Upstream builds the
/// <c>container-custom-renderers</c> fixture with <c>output: 'server'</c> and calls <c>app.render()</c> of a test adapter; here
/// <c>cb-container</c> is built with the jint adapter and every endpoint is requested from the host, so the container, the
/// renderers and the compiled components run in Jint.
/// <para>
/// Difference from upstream: every response also has to have status 200. The <c>src</c> of the scripts that the "direct
/// assets" endpoint writes is the module id of the script (a source path and a query, which is what the container returns when
/// it has no manifest); like upstream this only checks that there is one, since a browser cannot load it.
/// </para>
/// </summary>
[Trait("Upstream", "core/container.test.ts")]
public partial class ContainerTests(SiteHost sites)
{
    private const string Fixture = "cb-container";

    [GeneratedRegex(@"^<style>[\s\S]*\.asset[\s\S]*</style>")]
    private static partial Regex LeadingAssetStyle { get; }

    [GeneratedRegex(@"<style>\s*\.inline \{\s*color: blue;\s*\}\s*</style>")]
    private static partial Regex InlineStyle { get; }

    [GeneratedRegex("""<span>Child</span><script type="module" src="[^"]+"></script></div>""")]
    private static partial Regex ChildScript { get; }

    [GeneratedRegex("""<script type="module" src="[^"]+"></script>\z""")]
    private static partial Regex TrailingScript { get; }

    private async Task<string> RenderAsync(string path)
    {
        var site = await sites.GetAsync(Fixture);
        using var response = await site.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.ReadTextAsync();
    }

    [Fact(DisplayName = "the endpoint should return the HTML of the React component")]
    public async Task ReactEndpoint_ReturnsTheHtmlOfTheComponent()
    {
        var html = await RenderAsync("/react");

        Assert.Contains("I am a react button", html);
        Assert.DoesNotContain("<!DOCTYPE html>", html);
    }

    [Fact(DisplayName = "the endpoint should return the HTML of the React component, with DOCTYPE when rendered when partial is off")]
    public async Task ReactEndpoint_WithPartialOff_HasTheDoctype()
    {
        var html = await RenderAsync("/react-as-page");

        Assert.Contains("I am a react button", html);
        Assert.Contains("<!DOCTYPE html>", html);
    }

    [Fact(DisplayName = "the endpoint should return the HTML of the Vue component")]
    public async Task VueEndpoint_ReturnsTheHtmlOfTheComponent()
    {
        var html = await RenderAsync("/vue");

        Assert.Contains("I am a vue button", html);
        Assert.DoesNotContain("<!DOCTYPE html>", html);
    }

    [Fact(DisplayName = "Should render a component with directives")]
    public async Task ComponentWithDirectives_IsRendered()
    {
        var html = await RenderAsync("/button-directive");

        Assert.Contains("Button not rendered", html);
        Assert.Contains("I am a react button", html);
        Assert.DoesNotContain("<!DOCTYPE html>", html);
    }

    [Fact(DisplayName = "renders direct assets around compiled component HTML")]
    public async Task DirectAssets_AreRenderedAroundTheComponent()
    {
        var html = await RenderAsync("/assets");

        Assert.Matches(LeadingAssetStyle, html);
        Assert.Matches(InlineStyle, html);
        Assert.Matches(ChildScript, html);
        Assert.Matches(TrailingScript, html);
    }
}
