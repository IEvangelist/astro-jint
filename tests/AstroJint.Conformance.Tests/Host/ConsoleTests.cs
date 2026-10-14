using System.Net;
using System.Text;
using Microsoft.Extensions.Logging;

namespace AstroJint.Conformance.Host;

/// <summary>
/// The <c>console</c> of the script has no process streams to write to in this host; it feeds the log of the host
/// under its own category. Upstream has no tests for this. The aborted-request fixture reports with
/// <c>console.error</c> from its custom <c>src/fetch.ts</c> just before it reads a request body, which makes it a
/// probe for both the console and the fetch handler.
/// </summary>
public class ConsoleTests(SiteHost sites)
{
    [Fact]
    public async Task ConsoleOutputOfTheScript_ReachesTheLogOfTheHost()
    {
        var site = await sites.GetAsync("aborted-request");

        using var response = await site.SendAsync(
            HttpMethod.Post,
            "/",
            new StringContent("{\"ok\":true}", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("ok", await response.Content.ReadAsStringAsync());
        var entry = Assert.Single(site.Logs, entry => entry.Message == "reading-json-body");
        Assert.Equal("AstroJint.Console", entry.Category);
        Assert.Equal(LogLevel.Error, entry.Level);
    }
}
