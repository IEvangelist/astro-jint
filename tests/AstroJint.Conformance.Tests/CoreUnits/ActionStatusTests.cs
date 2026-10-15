using static AstroJint.Conformance.CoreUnits.AppActions;
using static AstroJint.Conformance.CoreUnits.AppRouting;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/actions/action-status.test.ts</c>: the <c>Astro.response.status</c> that a page sees is
/// 200 unless the locals carry the result of an action that failed, in which case it is the status of the error.
/// <para>
/// Differences from upstream: the component that captured <c>Astro.response.status</c> in a closure is the page
/// <c>action-status.astro</c> of the <c>app-actions</c> fixture, which prints it in <c>&lt;p id="status"&gt;</c>. The caller of
/// <c>App.render()</c> is the host: a <c>LocalsFactory</c> gives it the <c>_actionPayload</c> that upstream passed in the render
/// options, chosen by the request header <c>x-action-result</c>. The payloads are the exact output of
/// <c>serializeActionResult()</c> (the function of upstream's helpers), which cannot be called from the test, written down:
/// an <c>UNPROCESSABLE_CONTENT</c> error, and the data <c>null</c>. Each test also checks the status of the HTTP response.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/actions/action-status.test.ts")]
public class ActionStatusTests(SiteHost sites)
{
    [Fact(DisplayName = "uses default status when no action payload is present")]
    public async Task Page_WithoutAnActionResult_Is200()
    {
        var site = await sites.GetActionResultSiteAsync();

        var response = await site.SendRawAsync("/action-status");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("200", Text(response, "#status"));
    }

    [Fact(DisplayName = "uses the error status code when an action error result is in locals")]
    public async Task Page_WithAnActionError_HasTheStatusOfTheError()
    {
        var site = await sites.GetActionResultSiteAsync();

        var response = await site.SendRawAsync("/action-status", headers: ("x-action-result", "error-422"));

        Assert.Equal("422", Text(response, "#status"));
        Assert.Equal(422, response.StatusCode);
    }

    [Fact(DisplayName = "uses default status for a successful action data result")]
    public async Task Page_WithActionData_Is200()
    {
        var site = await sites.GetActionResultSiteAsync();

        var response = await site.SendRawAsync("/action-status", headers: ("x-action-result", "data-null"));

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("200", Text(response, "#status"));
    }
}
