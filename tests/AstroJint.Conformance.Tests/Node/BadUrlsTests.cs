namespace AstroJint.Conformance.Node;

/// <summary>Port of <c>packages/integrations/node/test/bad-urls.test.ts</c>.</summary>
[Trait("Upstream", "node/bad-urls.test.ts")]
public class BadUrlsTests(SiteHost sites)
{
    private const string Fixture = "bad-urls";

    // The request targets the upstream URLs become on the wire: the URL parser of fetch() turns backslashes into slashes and
    // percent-encodes non-ASCII, but keeps stray percent signs. The first target keeps its backslash.
    public static TheoryData<string> WeirdTargets =>
    [
        "/\\xfs.bxss.me%3Fastrojs.com/hello-world",
        "//xfs.bxss.me%3Fastrojs.com/hello-world",
        "/asdasdasd@ax_zX=.zxczas%F0%9F%90%A5%/%C3%BAadasd000%/",
        "/%",
        "/%80",
        "/%c",
        "/%c0%80",
        "/%20foobar%",
    ];

    [Theory(DisplayName = "Does not crash on bad urls")]
    [MemberData(nameof(WeirdTargets))]
    public async Task BadUrls_DoNotCrashTheHost(string target)
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync(target);

        Assert.True(
            response.StatusCode is 400 or 404 or 500,
            $"{target} returned {response.StatusCode} instead of 400, 404, or 500");
        Assert.Equal("<!DOCTYPE html>Hello!", await site.GetStringAsync("/"));
    }

    [Theory(DisplayName = "Does not crash on URLs with hash fragments")]
    [InlineData("/#")]
    [InlineData("/#foo")]
    [InlineData("/#/path")]
    public async Task HashFragments_AreStrippedBeforeTheRequestReachesTheHost(string url)
    {
        var site = await sites.GetAsync(Fixture);

        using var response = await site.GetAsync(url);

        Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
    }
}
