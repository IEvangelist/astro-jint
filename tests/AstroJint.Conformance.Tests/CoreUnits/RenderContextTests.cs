using Microsoft.Extensions.Logging;
using static AstroJint.Conformance.CoreUnits.AppActions;
using static AstroJint.Conformance.CoreUnits.AppLogging;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/render/render-context.test.ts</c>. Upstream builds a <c>FetchState</c> by hand (sets its
/// <c>routeData</c>, <c>skipMiddleware</c> and <c>status</c>, calls <c>getActionAPIContext()</c> and renders a component through
/// <c>handleMiddleware</c>) and reads what a <c>SpyLogger</c> recorded. A request reaches none of that, so all six tests are
/// adapted to the request flow that makes Astro set the same state.
/// <para>
/// The two <c>skipMiddleware</c> tests post an empty form (<c>?_action=countedForm</c>, upstream's <c>new FormData()</c>) to the
/// <c>app-actions</c> fixture, on a host with a single engine of its own so that the request that reads the counter
/// (<c>formCallCount</c>) sees the posts of the test. A state has <c>skipMiddleware</c> set when Astro renders an error page
/// again after the middleware failed for the first render of it. The middleware of the fixture throws for the header
/// <c>x-fail-middleware</c>, so the post to <c>/form-result</c> with that header is answered by <c>500.astro</c> (upstream's
/// 'Error page'), rendered without the middleware: were it rendered with it, the middleware would throw again and the response
/// would be a bare 500. Upstream forced <c>status = 404</c> on its state, the status here is the 500 of the request. The action
/// does not run, as the counter shows. The normal flow of the second test is the post to the same page without the header, which
/// runs the action once and shows its result through <c>Astro.getActionResult()</c>.
/// </para>
/// <para>
/// A <c>SpyLogger</c> cannot be put in the manifest of a host, so the loggers are read from what the destination writes: the
/// <c>console</c> of the script feeds the log of the host under the category <c>AstroJint.Console</c> (see <see cref="AppLogging"/>),
/// where the default console logger writes a time, then the level for a warning or an error (<c>[WARN]</c>, <c>[ERROR]</c>), then
/// the text, and a label would sit between the level and the text. The two <c>context.logger</c> tests call actions of the
/// fixture that use <c>context.logger</c> (the <c>ActionAPIContext</c> of upstream's <c>getActionAPIContext()</c>), and the two
/// <c>Astro.logger</c> tests request the pages <c>/logger/page</c> and <c>/logger/hello from page</c> of <c>app-render</c>, whose
/// component calls <c>Astro.logger</c>.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/render/render-context.test.ts")]
public class RenderContextTests(SiteHost sites)
{
    private static readonly (string Name, string Value) FailTheMiddleware = ("x-fail-middleware", "1");

    [Fact(DisplayName = "does not auto-execute form actions when skipMiddleware is true")]
    public async Task ErrorPageRenderedWithoutMiddleware_DoesNotAutoExecuteTheFormAction()
    {
        var site = await sites.GetSingleEngineSiteAsync("form-error-page");

        var response = await site.SendRawAsync(
            "/form-result?_action=countedForm", "POST", FormBodies.Multipart(), FormBodies.MultipartType, FailTheMiddleware);
        var count = await PostJsonAsync(site, "/_actions/formCallCount", "{}");

        Assert.Equal(500, response.StatusCode);
        Assert.Contains("Error page", response.Text, StringComparison.Ordinal);
        Assert.Equal("""[{"calls":1},0]""", count.Text);
    }

    [Fact(DisplayName = "auto-executes form actions when skipMiddleware is false")]
    public async Task Page_AutoExecutesTheFormAction()
    {
        var site = await sites.GetSingleEngineSiteAsync("form-page");

        var response = await PostFormAsync(site, "/form-result?_action=countedForm");
        var count = await PostJsonAsync(site, "/_actions/formCallCount", "{}");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("action result", View(response).GetProperty("data").GetString());
        Assert.Equal("""[{"calls":1},1]""", count.Text);
    }

    [Fact(DisplayName = "provides info/warn/error methods on context.logger")]
    public async Task ContextLogger_ProvidesInfoWarnAndErrorMethods()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await PostJsonAsync(site, "/_actions/contextLoggerMethods", "{}");

        Assert.Equal(200, response.StatusCode);
        Assert.Equal("object/function/function/function", Shown(response));
    }

    [Fact(DisplayName = "context.logger delegates to the pipeline logger")]
    public async Task ContextLogger_DelegatesToThePipelineLogger()
    {
        var site = await sites.GetActionsSiteAsync();

        var response = await PostJsonAsync(site, "/_actions/contextLoggerWrites", "{}");

        Assert.Equal(200, response.StatusCode);
        var info = site.ConsoleLine("info message");
        var warn = site.ConsoleLine("warn message");
        var error = site.ConsoleLine("error message");
        Assert.Equal(LogLevel.Information, info.Level);
        Assert.DoesNotContain("[", info.Message, StringComparison.Ordinal);
        Assert.Equal(LogLevel.Information, warn.Level); // the console destination uses console.info for everything below an error
        Assert.EndsWith("[WARN] warn message", warn.Message, StringComparison.Ordinal);
        Assert.Equal(LogLevel.Error, error.Level);
        Assert.EndsWith("[ERROR] error message", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Astro.logger is always available on the page global")]
    public async Task AstroLogger_IsAvailableOnThePage()
    {
        var site = await sites.GetAsync(AppRender.Fixture);

        var response = await site.SendRawAsync("/logger/page");

        Assert.Equal(200, response.StatusCode);
        Assert.Contains("<p>Logged</p>", response.Text, StringComparison.Ordinal);
        var info = site.ConsoleLine("page info");
        var warn = site.ConsoleLine("page warn");
        var error = site.ConsoleLine("page error");
        Assert.Equal(LogLevel.Information, info.Level);
        Assert.DoesNotContain("[", info.Message, StringComparison.Ordinal);
        Assert.Equal(LogLevel.Information, warn.Level);
        Assert.EndsWith("[WARN] page warn", warn.Message, StringComparison.Ordinal);
        Assert.Equal(LogLevel.Error, error.Level);
        Assert.EndsWith("[ERROR] page error", error.Message, StringComparison.Ordinal);
    }

    [Fact(DisplayName = "Astro.logger delegates to the pipeline logger")]
    public async Task AstroLogger_DelegatesToThePipelineLogger()
    {
        var site = await sites.GetAsync(AppRender.Fixture);

        var response = await site.SendRawAsync(LoggerPage("hello from page"));

        Assert.Equal(200, response.StatusCode);
        var line = site.ConsoleLine("hello from page");
        Assert.Equal(LogLevel.Information, line.Level);
        Assert.DoesNotContain("[", line.Message, StringComparison.Ordinal);
    }
}
