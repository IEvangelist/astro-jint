using System.Net;
using System.Text.Json;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/import-app.test.ts</c> (<c>Import astro/app</c>): <c>await import('astro/app')</c> must not throw. Upstream
/// runs it in the test process, which is Node. ADAPTED: the code that has to import it is the server bundle in the Jint engine, so the
/// <c>core-astro-basic</c> fixture has a page endpoint that is not upstream (<c>src/pages/import-app.ts</c>, on-demand in the
/// <c>output: 'server'</c> variant) which runs <c>await import('astro/app')</c> inside the engine on each request and returns the names it
/// exports, or a 500 with the error when the import throws. The test also checks that <c>App</c> and <c>BaseApp</c> are among the names,
/// which upstream does not (<c>assert.equal(true, true)</c>).
/// </summary>
[Trait("Upstream", "core/import-app.test.ts")]
public class ImportAppTests(SiteHost sites)
{
    private const string Fixture = "core-astro-basic";

    [Fact(DisplayName = "Import astro/app: Successfully imports astro/app")]
    public async Task AstroApp_ImportedByTheEngine_DoesNotThrow()
    {
        var site = await sites.GetAsync(Fixture, new { output = "server" });

        using var response = await site.GetAsync("/import-app");

        var body = await response.ReadTextAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, $"Importing astro/app should not throw an error: {body}");
        using var json = JsonDocument.Parse(body);
        Assert.True(json.RootElement.GetProperty("imported").GetBoolean());
        var exports = json.RootElement.GetProperty("exports").EnumerateArray().Select(name => name.GetString()).ToList();
        Assert.Contains("App", exports);
        Assert.Contains("BaseApp", exports);
    }
}
