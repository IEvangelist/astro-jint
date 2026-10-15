using System.Net;

namespace AstroJint.Conformance.CoreBuild;

/// <summary>
/// Port of <c>packages/astro/test/build-readonly-file.test.ts</c>: a read-only file in <c>public/</c> (what a checkout from a
/// version control that locks files leaves) must not stop the build. Upstream builds the <c>build-readonly-file</c> fixture twice,
/// as a static site without adapter and with a test adapter, makes <c>public/test.txt</c> read-only first and only checks that the
/// build finishes.
/// <para>
/// Differences from upstream: the adapter under test is part of every build, so the two builds are <c>output: 'static'</c> and
/// <c>output: 'server'</c> with it (both lay out <c>client</c> and <c>server</c>, which the adapter-less static build of upstream
/// does not). The output of the builds that earlier runs made, when the file was not read-only, is deleted first, so that both
/// builds happen now with the attribute set. Stricter than upstream: the file has to be in the output and the host has to serve
/// it, and the page of the fixture has to be served too, with the script that the build inlined into it.
/// </para>
/// </summary>
[Trait("Upstream", "core/build-readonly-file.test.ts")]
public class BuildReadonlyFileTests(SiteHost sites)
{
    private const string Fixture = "cb-readonly-file";

    private static readonly string Root = Path.Combine(Repository.FixturesRoot, Fixture);

    private static readonly Lazy<bool> EmptyOutputs = new(() =>
    {
        foreach (var entry in Directory.EnumerateFileSystemEntries(Root, "dist*"))
        {
            MakeWritable(entry);
            if (Directory.Exists(entry))
            {
                Directory.Delete(entry, recursive: true);
            }
            else
            {
                File.Delete(entry);
            }
        }

        return true;
    });

    /// <summary>Clears the read-only attribute of every file below <paramref name="entry"/> (a file or a folder).</summary>
    private static void MakeWritable(string entry)
    {
        var files = Directory.Exists(entry) ? Directory.EnumerateFiles(entry, "*", SearchOption.AllDirectories) : [entry];
        foreach (var file in files)
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
    }

    [Theory(DisplayName = "When a read-only file exists in /public: Gets successfully build")]
    [InlineData("static")]
    [InlineData("server")]
    public async Task AReadOnlyFileInPublic_DoesNotStopTheBuild(string output)
    {
        var publicFile = Path.Combine(Root, "public", "test.txt");
        Assert.True(EmptyOutputs.Value);

        File.SetAttributes(publicFile, FileAttributes.ReadOnly);
        try
        {
            var site = await sites.GetAsync(Fixture, new { output });

            Assert.True(site.PathExists("client/test.txt"), "the file of public/ should be in the output");
            using var file = await site.GetAsync("/test.txt");
            Assert.Equal(HttpStatusCode.OK, file.StatusCode);
            Assert.Equal("test", (await file.Content.ReadAsStringAsync()).Trim());
            Assert.StartsWith("text/plain", file.Content.Headers.ContentType?.ToString());

            var page = await PageAssets.PageAsync(site, "/");
            Assert.Contains(page.QuerySelectorAll("script"), script => script.TextContent.Contains("console.log") && script.TextContent.Contains("test"));
        }
        finally
        {
            File.SetAttributes(publicFile, FileAttributes.Normal);
            foreach (var entry in Directory.EnumerateFileSystemEntries(Root, "dist*"))
            {
                MakeWritable(entry);
            }
        }
    }
}
