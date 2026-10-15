using System.Net;
using System.Text.Json.Nodes;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/live-loaders.test.ts</c> on the <c>live-loaders</c> fixture (<c>core-live-loaders</c>; upstream's config uses
/// <c>@astrojs/node</c> as the adapter, here it is the Jint adapter). Every page of the fixture is on-demand and reads a live collection
/// through <c>getLiveCollection</c>/<c>getLiveEntry</c>. The <c>deepEqual</c> checks of the test are <see cref="JsonNode.DeepEquals"/> on the
/// JSON that <c>/api/</c> returns.
/// <list type="bullet">
/// <item>The five <c>Dev</c> tests start the Astro dev server (with a logger that collects the messages, which nothing asserts) and request
/// the pages from it. Their assertions do not depend on the dev server, so they run against the production build here: adapted.</item>
/// <item>The four <c>SSR</c> tests build the fixture and call <c>app.render</c>, which is the host: ported.</item>
/// </list>
/// </summary>
[Trait("Upstream", "core/live-loaders.test.ts")]
public class LiveLoadersTests(SiteHost sites)
{
    private const string Fixture = "core-live-loaders";

    private static JsonNode? Json(string text) => JsonNode.Parse(text);

    private static void AssertJson(string expected, JsonNode? actual) =>
        Assert.True(JsonNode.DeepEquals(Json(expected), actual), $"expected {expected} but was {actual?.ToJsonString()}");

    private static async Task<JsonNode> ApiAsync(ConformanceSite site, string path)
    {
        using var response = await site.GetAsync(path);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return JsonNode.Parse(await response.ReadTextAsync())!;
    }

    private static string EntryByString => """
        {"entry":{"id":"123","data":{"title":"Page 123","age":10,"collection":"liveStuff"},"rendered":{"html":"<h1>Page 123</h1><p>This is rendered content.</p>"},"cacheHint":{"tags":["page:123"],"lastModified":"2025-01-01T00:00:00.000Z"}},"cacheHint":{"tags":["page:123"],"lastModified":"2025-01-01T00:00:00.000Z"}}
        """;

    private static string EntryByObject(int age) => """
        {"entry":{"id":"456","data":{"title":"Page 456","age":@AGE@,"collection":"liveStuff"},"cacheHint":{"tags":["page:456"],"lastModified":"2025-01-01T00:00:00.000Z"}},"cacheHint":{"tags":["page:456"],"lastModified":"2025-01-01T00:00:00.000Z"}}
        """.Replace("@AGE@", age.ToString());

    [Fact(DisplayName = "Live content collections: Dev: can load live data")]
    public async Task Api_LoadsLiveData()
    {
        var site = await sites.GetAsync(Fixture);

        var data = await ApiAsync(site, "/api/");

        AssertJson(EntryByString, data["entryByString"]);
        AssertJson(EntryByObject(20), data["entryByObject"]);
        AssertJson("""
            {"entries":[{"id":"123","data":{"title":"Page 123","age":10},"rendered":{"html":"<h1>Page 123</h1><p>This is rendered content.</p>"}},{"id":"456","data":{"title":"Page 456","age":20}},{"id":"789","data":{"title":"Page 789","age":30}}],"cacheHint":{"tags":["page"],"lastModified":"2025-01-02T00:00:00.000Z"}}
            """, data["collection"]);
    }

    [Fact(DisplayName = "Live content collections: Dev: can load live data with dynamic filtering")]
    public async Task Api_PassesTheDynamicFilterToTheLoader()
    {
        var site = await sites.GetAsync(Fixture);

        var data = await ApiAsync(site, "/api/?addToAge=5");

        AssertJson(EntryByObject(25), data["entryByObject"]);
        AssertJson("""
            [{"id":"123","data":{"title":"Page 123","age":15,"collection":"liveStuff"},"rendered":{"html":"<h1>Page 123</h1><p>This is rendered content.</p>"}},{"id":"456","data":{"title":"Page 456","age":25,"collection":"liveStuff"}},{"id":"789","data":{"title":"Page 789","age":35,"collection":"liveStuff"}}]
            """, data["collection"]!["entries"]);
    }

    [Fact(DisplayName = "Live content collections: Dev: returns an error for invalid data")]
    public async Task Api_InvalidDataIsAValidationError()
    {
        var site = await sites.GetAsync(Fixture);

        var data = await ApiAsync(site, "/api/?returnInvalid=true&addToAge=1");

        var error = data["collection"]!["error"]!;
        Assert.Contains("data does not match the collection schema", error["message"]!.GetValue<string>());
        Assert.Equal("LiveCollectionValidationError", error["name"]!.GetValue<string>());
    }

    [Fact(DisplayName = "Live content collections: Dev: old API throws helpful errors for live collections")]
    public async Task OldApi_ThrowsHelpfulErrorsForLiveCollections_OnTheProductionBuild()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/test-old-api");
        var data = JsonNode.Parse(await response.ReadTextAsync())!;

        Assert.Contains("Use getLiveCollection() instead of getCollection()", data["error"]!.GetValue<string>());
    }

    [Fact(DisplayName = "Live content collections: Dev: can render live entry with rendered content")]
    public async Task Rendered_RendersTheLiveEntry_OnTheProductionBuild()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/rendered");
        var html = await response.ReadTextAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<h1>Page 123</h1>", html);
        Assert.Contains("<p>This is rendered content.</p>", html);
    }

    [Fact(DisplayName = "Live content collections: SSR: loads live data")]
    public async Task Ssr_LoadsLiveData()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/api/");
        var data = JsonNode.Parse(await response.ReadTextAsync())!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertJson(EntryByString, data["entryByString"]);
    }

    [Fact(DisplayName = "Live content collections: SSR: loads live data with dynamic filtering")]
    public async Task Ssr_PassesTheDynamicFilterToGetEntry()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/api/?addToAge=5");
        var data = JsonNode.Parse(await response.ReadTextAsync())!;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        AssertJson(EntryByObject(25), data["entryByObject"]);
    }

    [Fact(DisplayName = "Live content collections: SSR: old API throws helpful errors for live collections")]
    public async Task Ssr_OldApiThrowsHelpfulErrorsForLiveCollections()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/test-old-api");
        var data = JsonNode.Parse(await response.ReadTextAsync())!;

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains("Use getLiveCollection() instead of getCollection()", data["error"]!.GetValue<string>());
    }

    [Fact(DisplayName = "Live content collections: SSR: can render live entry with rendered content")]
    public async Task Ssr_RendersTheLiveEntry()
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync("/rendered");
        var html = await response.ReadTextAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("<h1>Page 123</h1>", html);
        Assert.Contains("<p>This is rendered content.</p>", html);
    }
}
