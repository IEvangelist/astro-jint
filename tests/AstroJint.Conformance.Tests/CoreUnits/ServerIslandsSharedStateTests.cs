namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/server-islands/shared-state.test.ts</c>, which feeds <c>ServerIslandsState</c> (the state
/// that the Vite plugin fills while it discovers the <c>server:defer</c> components of a build) with inline records. Three of the
/// four tests are adapted: the state is a build-time object, so what its calls decide is observed in the build that the fixture
/// <c>app-server-islands</c> makes and in the endpoint that serves the islands it names. <c>names.astro</c> imports one component
/// twice (<c>Twin</c> and <c>AlsoTwin</c>, from <c>twin-a/Twin.astro</c>) and another one that has the same file name and the same
/// local name (<c>twin-b/Twin.astro</c>, imported by <c>TwinHolder.astro</c>), so the page has three islands of two components. The
/// island that a page names is the one that its script fetches: <c>/_server-islands/&lt;name&gt;</c>. The fourth test is not
/// applicable (see the last comment).
/// </summary>
[Trait("Upstream", "core-units/server-islands/shared-state.test.ts")]
public class ServerIslandsSharedStateTests(SiteHost sites)
{
    private async Task<(ConformanceSite Site, IslandScript[] Islands)> OpenAsync()
    {
        var site = await sites.GetServerIslandsSiteAsync();
        var page = await site.SendRawAsync("/names");
        Assert.Equal(200, page.StatusCode);

        var islands = AppServerIslands.Islands(page.Text);
        Assert.Equal(3, islands.Length);
        return (site, islands);
    }

    private static async Task<string> FetchTextAsync(ConformanceSite site, IslandScript island)
    {
        using var response = await island.FetchAsync(site);
        Assert.Equal(200, (int)response.StatusCode);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact(DisplayName = "tracks island discovery and deduplicates by resolved path")]
    public async Task Discovery_DeduplicatesByResolvedPath()
    {
        var (site, islands) = await OpenAsync();

        // Twin and AlsoTwin are one component: the first record of a resolved path wins, so that both are the same island. The
        // third island is another component. The build knows two islands for the three that the page has.
        Assert.Equal(islands[0].Name, islands[1].Name);
        Assert.NotEqual(islands[0].Name, islands[2].Name);
        Assert.Equal(2, islands.Select(island => island.Name).Distinct().Count());

        // Each of the two tags still sends its own props to the island that they share.
        var first = ConformanceSite.ParseHtml(await FetchTextAsync(site, islands[0]));
        var second = ConformanceSite.ParseHtml(await FetchTextAsync(site, islands[1]));
        Assert.Equal("""{"which":"a"}""", first.QuerySelector("#twin-a")?.GetAttribute("data-props"));
        Assert.Equal("""{"which":"a again"}""", second.QuerySelector("#twin-a")?.GetAttribute("data-props"));
    }

    [Fact(DisplayName = "does not deduplicate when resolved paths differ")]
    public async Task Discovery_KeepsComponentsThatShareALocalNameApart()
    {
        var (site, islands) = await OpenAsync();

        // Two files with the same local name are two islands, and each name serves its own component.
        var twinA = ConformanceSite.ParseHtml(await FetchTextAsync(site, islands[0]));
        var twinB = ConformanceSite.ParseHtml(await FetchTextAsync(site, islands[2]));

        Assert.NotEqual(islands[0].Name, islands[2].Name);
        Assert.Equal("Twin A", twinA.QuerySelector("#twin-a")?.TextContent);
        Assert.Null(twinA.QuerySelector("#twin-b"));
        Assert.Equal("Twin B", twinB.QuerySelector("#twin-b")?.TextContent);
        Assert.Null(twinB.QuerySelector("#twin-a"));
    }

    [Fact(DisplayName = "generates unique island names and map sources")]
    public async Task Discovery_NamesIslandsUniquelyAndMapsEachNameToItsChunk()
    {
        var (site, islands) = await OpenAsync();

        // The second component with the local name Twin gets a number. Which of the two components that is depends on the order in
        // which the build discovers them, so the test only asks for the two names and for one component behind each.
        Assert.Equal(["Twin", "Twin1"], islands.Select(island => island.Name).Distinct().Order());

        // The map of the server (name to import of the chunk) has both: each name loads its own component...
        var served = new List<(string Name, string Component)>();
        foreach (var island in islands)
        {
            var html = await FetchTextAsync(site, island);
            served.Add((island.Name, html.Contains("id=\"twin-a\"", StringComparison.Ordinal) ? "twin-a" : "twin-b"));
        }

        Assert.Equal(2, served.Distinct().Count());
        Assert.Equal(2, served.Select(entry => entry.Name).Distinct().Count());
        Assert.Equal(2, served.Select(entry => entry.Component).Distinct().Count());

        // ... and no other name is in it.
        var unknown = await site.SendRawAsync(AppServerIslands.IslandTarget("Twin2"));
        Assert.Equal(404, unknown.StatusCode);
    }

    // Not applicable: 'creates import map source from rollup reference ids' calls createImportMapSourceFromReferences() with
    // fake reference ids and checks the text that it generates. The build does that with real ones, and every island request of the
    // tests above (and of the other tests of this folder) resolves its chunk through the map that it generated.
}
