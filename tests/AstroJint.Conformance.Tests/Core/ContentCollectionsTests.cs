using System.Net;
using AngleSharp.Dom;
using Microsoft.Extensions.Logging;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of the <c>SSR integration</c> block of <c>packages/astro/test/content-collections.test.ts</c> on the <c>content-ssr-integration</c>
/// fixture (<c>core-content-ssr-integration</c>): an on-demand site with the MDX integration whose <c>posts/[...slug].astro</c> page calls
/// <c>getEntry()</c> and <c>render()</c> of <c>astro:content</c>. The vite plugin of the test (<c>preventNodeBuiltinDependencyPlugin</c>,
/// which fails the build when <c>astro:content</c> pulls in <c>node:fs</c>, <c>node:url</c>, <c>node:worker_threads</c> or <c>node:path</c>) is
/// part of the fixture's config, so every build of it runs that check.
/// <para>
/// The four tests were GAP-CORE-1 gaps while the server build of the adapter was a single <c>entry.mjs</c> (a build that renders a collection
/// entry kept the manifest placeholder in it, so every request answered 500). The adapter splits that build into chunks since dad60e1, and
/// they pass. The last one (<c>getEntryBySlug</c> answers 500) would also pass if every request failed, so besides the status the port
/// asserts that the host logged the deprecation error of the page (not asserted upstream).
/// </para>
/// <para>
/// <c>With spaces in path</c> builds a project whose folder name has spaces and asserts that the build does not throw. It is adapted: the
/// fixture <c>core-content with spaces in folder name</c> is built with the adapter (the harness throws when a build fails), once with the page
/// prerendered and once on demand (JSON config <c>output: 'server'</c>, so the engine renders it from a server bundle in a path with spaces),
/// and the page is requested from the host.
/// </para>
/// <para>
/// The other 20 <c>it</c>s of the file are not ported (see <c>docs/conformance/core.md</c>): they build static fixtures with no adapter (the
/// <c>Query</c>, <c>Static paths integration</c>, <c>Base configuration</c> and <c>Mutation</c> blocks assert on the files of the output or
/// on JSON built into it) or only expect a build to fail (<c>With legacy config</c>, <c>With empty markdown file</c>, <c>With numbers for
/// IDs</c>) or to produce a page of a static build (<c>With empty collections directory</c>); none of them touches the on-demand runtime.
/// </para>
/// </summary>
[Trait("Upstream", "core/content-collections.test.ts")]
public class ContentCollectionsTests(SiteHost sites)
{
    private const string Fixture = "core-content-ssr-integration";
    private const string SpacesFixture = "core-content with spaces in folder name";

    private static readonly (string Slug, string Title, string Element, string Content)[] Posts =
    [
        ("first-post", "First post", "blockquote", "First post loaded: yes!"),
        ("second-post", "Second post", "blockquote", "Second post loaded: yes!"),
        ("third-post", "Third post", "blockquote", "Third post loaded: yes!"),
        ("using-mdx", "Using MDX", "a[href=\"#\"]", "Embedded component in MDX"),
    ];

    private static string TextOf(IDocument html, string selector) =>
        string.Concat(html.QuerySelectorAll(selector).Select(element => element.TextContent));

    [Fact(DisplayName = "Content Collections: With spaces in path: Does not throw")]
    public async Task ProjectFolderWithSpaces_BuildsAndServesItsPage()
    {
        var prerendered = await sites.GetAsync(SpacesFixture);
        var onDemand = await sites.GetAsync(SpacesFixture, new { output = "server" });

        foreach (var site in new[] { prerendered, onDemand })
        {
            // Not asserted upstream: the test is about the folder, so make sure it still has the space.
            Assert.Contains(" ", site.DistPath);
            using var response = await site.GetAsync("/");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var html = await response.ReadHtmlAsync();
            Assert.Equal("It's content time!", html.Title);
        }
        Assert.True(prerendered.PathExists("client/index.html"), "the static build prerenders the page");
        Assert.False(onDemand.PathExists("client/index.html"), "the server build leaves the page to the engine");
    }

    [Fact(DisplayName = "Content Collections: SSR integration: Responds 200 for expected pages")]
    public async Task Posts_RespondWith200()
    {
        var site = await sites.GetAsync(Fixture);

        foreach (var post in Posts)
        {
            using var response = await site.GetAsync("/posts/" + post.Slug);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact(DisplayName = "Content Collections: SSR integration: Renders titles")]
    public async Task Posts_RenderTheirTitles()
    {
        var site = await sites.GetAsync(Fixture);

        foreach (var post in Posts)
        {
            using var response = await site.GetAsync("/posts/" + post.Slug);
            var html = await response.ReadHtmlAsync();

            Assert.Equal(post.Title, TextOf(html, "h1"));
        }
    }

    [Fact(DisplayName = "Content Collections: SSR integration: Renders content")]
    public async Task Posts_RenderTheirContent()
    {
        var site = await sites.GetAsync(Fixture);

        foreach (var post in Posts)
        {
            using var response = await site.GetAsync("/posts/" + post.Slug);
            var html = await response.ReadHtmlAsync();

            Assert.Equal(post.Content, TextOf(html, post.Element).Trim());
        }
    }

    [Fact(DisplayName = "Content Collections: SSR integration: Throws deprecation error when using getEntryBySlug")]
    public async Task DeprecatedGetEntryBySlug_Responds500()
    {
        var site = await sites.GetAsync(Fixture);
        var logStart = site.Logs.Count;

        using var response = await site.GetAsync("/deprecated-getentrybyslug");

        // Should return 500 status when getEntryBySlug is used with new content collections
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        // Not asserted upstream: the 500 is the deprecation error of the page and not a failure to start the engine.
        Assert.Contains(site.Logs.Skip(logStart), entry => entry.Level >= LogLevel.Error && entry.Message.Contains("getEntryBySlug"));
    }
}
