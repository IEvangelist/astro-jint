using System.Net;

namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/sql-import.test.ts</c>: a page can import a <c>.sql</c> file and gets its text.
/// <para>
/// The upstream test runs against the preview of the workerd runtime, where <c>.sql</c> is a text module; the page of the shared
/// fixture <c>cf-pages</c> is requested from the host instead.
/// The page has a fixture of its own, <c>cf-sql</c>, because the build of it fails (GAP-CF-1) and would take the shared
/// fixture <c>cf-pages</c> down with it.
/// </para>
/// </summary>
[Trait("Upstream", "cloudflare/sql-import.test.ts")]
public class SqlImportTests(SiteHost sites)
{
   private const string Fixture = "cf-sql";

   [Fact(DisplayName = "SQL Import: build: can import .sql files", Skip = "GAP-CF-1: the build of a page that imports a .sql file fails (Vite parses src/query.sql as JavaScript: PARSE_ERROR 'Expected a semicolon or an implicit semicolon after a statement', src/query.sql:1:14); the Cloudflare adapter gets .sql as a text module from workerd")]
   public async Task SqlFile_CanBeImported()
   {
       var site = await sites.GetAsync(Fixture);

       using var response = await site.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var query = (await response.ReadHtmlAsync()).QuerySelector("#query")?.TextContent;
        Assert.NotNull(query);
        Assert.Contains("SELECT * FROM users", query);
    }
}
