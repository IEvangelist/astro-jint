using System.Net;

namespace AstroJint.Conformance.CoreBuild;

/// <summary>
/// Port of <c>packages/astro/test/underscore-in-folder-name.test.ts</c>: a project whose folder name starts with an underscore
/// still builds the page that an integration injects from outside <c>src/pages</c> (Astro keeps files below a folder that starts
/// with an underscore out of the routes, and must not apply that to the folder of the project).
/// <para>
/// Differences from upstream: the folder of the fixture is <c>_cb-underscore</c>, and the fake library sits next to the config
/// (<c>node_modules</c> is ignored by git here), which keeps what the test is about: an injected route outside
/// <c>src/pages</c> in a project path with an underscore. The manifest of the test adapter that lists <c>/index.html</c> and
/// <c>/404.html</c> becomes the listing of <c>dist/client</c>, and the test also requests the pages from the host.
/// </para>
/// </summary>
[Trait("Upstream", "core/underscore-in-folder-name.test.ts")]
public class UnderscoreInFolderNameTests(SiteHost sites)
{
    [Fact(DisplayName = "includes page from node_modules/fake-astro-library")]
    public async Task IncludesThePageFromTheFakeLibrary()
    {
        var site = await sites.GetAsync("_cb-underscore");

        Assert.True(site.PathExists("client/index.html"), "the page of the project should be built");
        Assert.True(site.PathExists("client/404.html"), "the page injected by the library should be built");

        using var index = await site.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, index.StatusCode);
        Assert.Contains("Testing", await index.Content.ReadAsStringAsync());
        using var missing = await site.GetAsync("/does-not-exist");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Contains("Test 404", await missing.Content.ReadAsStringAsync());
    }
}
