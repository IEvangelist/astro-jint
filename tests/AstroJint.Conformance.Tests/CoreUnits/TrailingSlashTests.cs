namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/app/trailing-slash.test.ts</c>: the redirects that <c>App.render()</c> answers for a
/// request whose trailing slash does not match <c>trailingSlash</c>, through the <c>app-pages</c> fixture.
/// <para>
/// Differences from upstream: the pages come from a real build instead of a mock manifest. Upstream patterns accept
/// <c>/another</c> and <c>/another/</c> in every mode, here Astro builds the pattern that the mode asks for. The requests go
/// over a socket with the target as written, because <c>HttpClient</c> would parse <c>//target.example/path</c> as a
/// network-path reference. The fixture has an <c>index</c> page and a custom 404 page, and everything in it is rendered
/// per request. Every upstream title that repeats in the file carries its <c>describe</c> title in front.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/app/trailing-slash.test.ts")]
public class TrailingSlashTests(SiteHost sites)
{
    private const string Fixture = "app-pages";

    private static readonly object Always = new { trailingSlash = "always" };
    private static readonly object Never = new { trailingSlash = "never" };
    private static readonly object NeverWithBase = new { trailingSlash = "never", @base = "/mybase" };

    public static TheoryData<string> AlwaysInternalPaths =>
    [
        "/_astro/something",
        "/_image?url=http://example.com/foo.jpg",
        "/_server-islands/foo",
        "/_actions/foo",
        "/.netlify/image?url=http://example.com/foo.jpg",
        "//target.example/path",
    ];

    public static TheoryData<string> NeverInternalPaths =>
    [
        "/_astro/something/",
        "/_image/?url=http://example.com/foo.jpg",
        "/_server-islands/foo/",
        "/_actions/foo/",
        "/.netlify/image/?url=http://example.com/foo.jpg",
        "//target.example/path/",
    ];

    // Upstream appends one more slash to each of these.
    public static TheoryData<string> IgnoreInternalPaths =>
    [
        "/_astro/something///",
        "/_image//?url=http://example.com/foo.jpg/",
        "/_server-islands/foo///",
        "/_actions/foo///",
        "/.netlify/image//?url=http://example.com/foo.jpg/",
        "//target.example/path///",
    ];

    private async Task<RawResponse> RequestAsync(object? config, string target, string method = "GET")
    {
        var site = await sites.GetAsync(Fixture, config);
        return await site.SendRawAsync(target, method, method == "GET" ? null : []);
    }

    private async Task AssertRedirectAsync(object? config, string target, string location, int status = 301, string method = "GET")
    {
        var response = await RequestAsync(config, target, method);

        Assert.Equal(status, response.StatusCode);
        Assert.Equal(location, response.Header("location"));
    }

    private async Task AssertStatusAsync(object? config, string target, int status)
    {
        var response = await RequestAsync(config, target);

        Assert.Equal(status, response.StatusCode);
    }

    private async Task AssertNotRedirectedAsync(object? config, string target)
    {
        var response = await RequestAsync(config, target);

        Assert.NotEqual(301, response.StatusCode);
    }

    // trailingSlash: always

    [Fact(DisplayName = "Redirects to add a trailing slash")]
    public Task Always_PathWithoutTrailingSlash_RedirectsToAddIt() =>
        AssertRedirectAsync(Always, "/another", "/another/");

    [Fact(DisplayName = "trailingSlash: always: Redirects to collapse multiple trailing slashes")]
    public Task Always_MultipleTrailingSlashes_RedirectsToCollapseThem() =>
        AssertRedirectAsync(Always, "/another///", "/another/");

    [Fact(DisplayName = "Redirects to collapse multiple trailing slashes with query param")]
    public Task Always_MultipleTrailingSlashesWithQuery_RedirectsAndKeepsTheQuery() =>
        AssertRedirectAsync(Always, "/another///?hello=world", "/another/?hello=world");

    [Fact(DisplayName = "Does not redirect to collapse multiple internal slashes")]
    public Task Always_InternalSlashes_AreNotCollapsed() =>
        AssertStatusAsync(Always, "/another///path/", 404);

    [Fact(DisplayName = "Does not redirect trailing slashes on query params")]
    public Task Always_TrailingSlashesInTheQuery_AreIgnored() =>
        AssertStatusAsync(Always, "/another/?hello=world///", 200);

    [Fact(DisplayName = "trailingSlash: always: Does not redirect when trailing slash is present")]
    public Task Always_PathWithTrailingSlash_IsNotRedirected() =>
        AssertStatusAsync(Always, "/another/", 200);

    [Fact(DisplayName = "trailingSlash: always: Redirects with query params")]
    public Task Always_QueryParams_AreKeptByTheRedirect() =>
        AssertRedirectAsync(Always, "/another?foo=bar", "/another/?foo=bar");

    [Fact(DisplayName = "Does not redirect with query params when trailing slash is present")]
    public Task Always_PathWithTrailingSlashAndQuery_IsNotRedirected() =>
        AssertStatusAsync(Always, "/another/?foo=bar", 200);

    [Fact(DisplayName = "Redirects subdirectories to add a trailing slash")]
    public Task Always_Subdirectory_RedirectsToAddTheSlash() =>
        AssertRedirectAsync(Always, "/sub/path", "/sub/path/");

    [Fact(DisplayName = "Does not redirect requests for files")]
    public Task Always_RequestForAFile_IsNotRedirected() =>
        AssertStatusAsync(Always, "/favicon.ico", 404);

    [Fact(DisplayName = "Does not redirect requests for files in subdirectories")]
    public Task Always_RequestForAFileInASubdirectory_IsNotRedirected() =>
        AssertStatusAsync(Always, "/sub/favicon.ico", 404);

    [Fact(DisplayName = "Does redirect if the dot is in a directory name")]
    public Task Always_DotInADirectoryName_StillRedirects() =>
        AssertRedirectAsync(Always, "/dot.in.directory/path", "/dot.in.directory/path/");

    [Theory(DisplayName = "trailingSlash: always: Does not redirect internal paths")]
    [MemberData(nameof(AlwaysInternalPaths))]
    public Task Always_InternalPaths_AreNotRedirected(string target) =>
        AssertNotRedirectedAsync(Always, target);

    [Fact(DisplayName = "trailingSlash: always: Redirects POST requests")]
    public Task Always_PostRequest_RedirectsWith308() =>
        AssertRedirectAsync(Always, "/another", "/another/", status: 308, method: "POST");

    // trailingSlash: never

    [Fact(DisplayName = "Redirects to remove a trailing slash")]
    public Task Never_PathWithTrailingSlash_RedirectsToRemoveIt() =>
        AssertRedirectAsync(Never, "/another/", "/another");

    [Fact(DisplayName = "trailingSlash: never: Redirects to collapse multiple trailing slashes")]
    public Task Never_MultipleTrailingSlashes_RedirectsToRemoveThem() =>
        AssertRedirectAsync(Never, "/another///", "/another");

    [Fact(DisplayName = "trailingSlash: never: Does not redirect when trailing slash is absent")]
    public Task Never_PathWithoutTrailingSlash_IsNotRedirected() =>
        AssertStatusAsync(Never, "/another", 200);

    [Fact(DisplayName = "trailingSlash: never: Redirects with query params")]
    public Task Never_QueryParams_AreKeptByTheRedirect() =>
        AssertRedirectAsync(Never, "/another/?foo=bar", "/another?foo=bar");

    [Fact(DisplayName = "Does not redirect with query params when trailing slash is absent")]
    public Task Never_PathWithoutTrailingSlashAndQuery_IsNotRedirected() =>
        AssertStatusAsync(Never, "/another?foo=bar", 200);

    [Fact(DisplayName = "Does not redirect when there's a slash at the end of query params")]
    public Task Never_SlashAtTheEndOfTheQuery_IsIgnored() =>
        AssertStatusAsync(Never, "/another?foo=bar/", 200);

    [Fact(DisplayName = "Redirects subdirectories to remove a trailing slash")]
    public Task Never_Subdirectory_RedirectsToRemoveTheSlash() =>
        AssertRedirectAsync(Never, "/sub/path/", "/sub/path");

    [Fact(DisplayName = "Redirects even if there's a dot in the directory name")]
    public Task Never_DotInTheName_StillRedirects() =>
        AssertRedirectAsync(Never, "/favicon.ico/", "/favicon.ico");

    [Theory(DisplayName = "trailingSlash: never: Does not redirect internal paths")]
    [MemberData(nameof(NeverInternalPaths))]
    public Task Never_InternalPaths_AreNotRedirected(string target) =>
        AssertNotRedirectedAsync(Never, target);

    [Fact(DisplayName = "trailingSlash: never: Redirects POST requests")]
    public Task Never_PostRequest_RedirectsWith308() =>
        AssertRedirectAsync(Never, "/another/", "/another", status: 308, method: "POST");

    // trailingSlash: never with base path

    [Fact(DisplayName = "Redirects to remove a trailing slash on base path")]
    public Task NeverWithBase_BasePathWithTrailingSlash_RedirectsToRemoveIt() =>
        AssertRedirectAsync(NeverWithBase, "/mybase/", "/mybase");

    [Fact(DisplayName = "Does not redirect when base path has no trailing slash")]
    public async Task NeverWithBase_BasePathWithoutTrailingSlash_IsNotRedirected()
    {
        var response = await RequestAsync(NeverWithBase, "/mybase");

        Assert.NotEqual(301, response.StatusCode);
        Assert.NotEqual(308, response.StatusCode);
    }

    [Fact(DisplayName = "Redirects to remove trailing slash on sub-paths with base")]
    public Task NeverWithBase_SubPathWithTrailingSlash_RedirectsToRemoveIt() =>
        AssertRedirectAsync(NeverWithBase, "/mybase/another/", "/mybase/another");

    [Fact(DisplayName = "Does not redirect sub-paths without trailing slash with base")]
    public Task NeverWithBase_SubPathWithoutTrailingSlash_IsNotRedirected() =>
        AssertStatusAsync(NeverWithBase, "/mybase/another", 200);

    // trailingSlash: ignore, which is what the fixture does when nothing else is configured

    [Fact(DisplayName = "trailingSlash: ignore: Redirects to collapse multiple trailing slashes")]
    public Task Ignore_MultipleTrailingSlashes_RedirectsToACollapsedOne() =>
        AssertRedirectAsync(null, "/another///", "/another/");

    [Fact(DisplayName = "trailingSlash: ignore: Does not redirect when trailing slash is absent")]
    public Task Ignore_PathWithoutTrailingSlash_IsNotRedirected() =>
        AssertStatusAsync(null, "/another", 200);

    [Fact(DisplayName = "trailingSlash: ignore: Does not redirect when trailing slash is present")]
    public Task Ignore_PathWithTrailingSlash_IsNotRedirected() =>
        AssertStatusAsync(null, "/another/", 200);

    [Theory(DisplayName = "trailingSlash: ignore: Does not redirect internal paths")]
    [MemberData(nameof(IgnoreInternalPaths))]
    public Task Ignore_InternalPaths_AreNotRedirected(string target) =>
        AssertNotRedirectedAsync(null, target);
}
