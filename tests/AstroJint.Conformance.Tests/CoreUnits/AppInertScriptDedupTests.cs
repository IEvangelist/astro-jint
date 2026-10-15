using static AstroJint.Conformance.CoreUnits.AppRender;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// Port of <c>packages/astro/test/units/app/inert-script-dedup.test.ts</c>: the render instruction of a client directive and the
/// one of the server island runtime, rendered first inside a <c>&lt;template&gt;</c> and then outside of it, emit their script
/// both times, because what a template holds is inert and must not use up the deduplication of the script that a page needs.
/// <para>
/// Both tests use real pages: <c>inert-hydration</c> and <c>inert-server-island-runtime</c> of the <c>app-render</c> fixture are
/// upstream's two inline pages (<c>createRenderInstruction()</c> in the frontmatter, the instruction in a <c>&lt;template&gt;</c>
/// and in a <c>&lt;div&gt;</c>; the compiler writes the <c>templateEnter()</c> and <c>templateExit()</c> calls that upstream has
/// by hand). The server island test is ported as it is. The directive test is adapted: upstream puts a stub script
/// (<c>console.log("directive")</c>) in the manifest of the test, and a built site has the real script of the <c>load</c>
/// directive, so the test counts that script (<c>(self.Astro||(self.Astro={})).load=</c>) and checks where the two copies are.
/// The probe of <c>InertScriptDedupTests</c> covers the other cases of the render instructions.
/// </para>
/// </summary>
[Trait("Upstream", "core-units/app/inert-script-dedup.test.ts")]
public class AppInertScriptDedupTests(SiteHost sites)
{
    // A piece of the script of the load directive that no other script of the page has.
    private const string LoadDirective = "(self.Astro||(self.Astro={})).load=";

    [Fact(DisplayName = "keeps hydration prescripts available after template content")]
    public async Task HydrationPrescripts_AreAvailableAfterTemplateContent()
    {
        var html = await GetPageAsync("/inert-hydration");

        Assert.Equal(2, CountOccurrences(html, LoadDirective));
        var endOfTemplate = html.IndexOf("</template>", StringComparison.Ordinal);
        Assert.True(html.IndexOf(LoadDirective, StringComparison.Ordinal) < endOfTemplate);
        Assert.True(html.LastIndexOf(LoadDirective, StringComparison.Ordinal) > endOfTemplate);
    }

    [Fact(DisplayName = "does not consume server-island runtime dedup inside template content")]
    public async Task ServerIslandRuntime_InsideTemplateContent_DoesNotConsumeTheDedup()
    {
        var html = await GetPageAsync("/inert-server-island-runtime");

        Assert.Equal(2, CountOccurrences(html, "replaceServerIsland("));
    }

    private async Task<string> GetPageAsync(string path)
    {
        var site = await sites.GetAsync(Fixture);

        var response = await site.SendRawAsync(path);

        Assert.Equal(200, response.StatusCode);
        return response.Text;
    }

    private static int CountOccurrences(string content, string needle) => content.Split(needle).Length - 1;
}
