using System.Net;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/ts-astro-import.test.ts</c>. The upstream tests build a fixture where
/// a <c>.ts</c> module default-imports <c>.astro</c> components after they cleared the Vite cache, and read the log of the
/// build for two messages. The tests use the page <c>/ts-import</c> of the shared fixture <c>cf-content</c> (it renders
/// <c>components.Outer</c> of <c>src/lib/ui.ts</c>) and the log of its build (<c>BuildLog</c>); the harness keeps the Vite cache between
/// builds, where upstream clears it, so the dependency optimizer may have less to find.
/// <list type="bullet">
/// <item>Both tests are adapted: the log of the build has neither message, and the first test also asks the host for the
/// page, which has the markup of the nested components.</item>
/// </list>
/// </summary>
[Trait("Upstream", "cloudflare/ts-astro-import.test.ts")]
public class TsAstroImportTests(SiteHost sites)
{
    [Fact(DisplayName = "should not produce \"No matching export\" error when a .ts module default-imports a .astro component")]
    public async Task ProducesNoMatchingExportError()
    {
        var site = await sites.GetAsync(CfContent.Fixture);

        var noMatchingExport = CfContent.BuildLogLines(site, "No matching export", "html:", "for import \"default\"").FirstOrDefault();

        Assert.True(
            noMatchingExport is null,
            $"Should not see \"No matching export in 'html:...' for import 'default'\" message, but got: {noMatchingExport}");

        using var response = await site.GetAsync("/ts-import");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var document = await response.ReadHtmlAsync();
        Assert.Equal("hello from inner", document.QuerySelector("div > span")?.TextContent);
    }

    [Fact(DisplayName = "should complete dependency scanning successfully")]
    public async Task CompletesDependencyScanning()
    {
        var site = await sites.GetAsync(CfContent.Fixture);

        var scanFailed = CfContent.BuildLogLines(site, "Failed to run dependency scan").FirstOrDefault();

        Assert.True(scanFailed is null, $"Should not see \"Failed to run dependency scan\" message, but got: {scanFailed}");
    }
}
