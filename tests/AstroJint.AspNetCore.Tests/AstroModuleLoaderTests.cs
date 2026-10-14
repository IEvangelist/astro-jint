using System.Net;
using System.Text.Json;
using Jint;
using Jint.Runtime.Modules;

namespace AstroJint.AspNetCore.Tests;

/// <summary>
/// Astro emits chunks next to the entry of the server bundle when a site has server islands or a page imports a
/// module dynamically, so the host has to load more than one file.
/// </summary>
public class AstroModuleLoaderTests
{
    private const string Entry = """
        import { name, nextId } from './chunks/shared.mjs';
        import { describe, importFromNested } from './chunks/nested/describe.mjs';

        const json = (value) => new Response(JSON.stringify(value), { headers: { 'content-type': 'application/json' } });

        const attempt = async (load) => {
          try {
            return { ok: true, value: await load() };
          } catch (error) {
            return { ok: false, error: String((error && error.message) || error) };
          }
        };

        export default {
          async fetch(request) {
            const url = new URL(request.url);
            const specifier = url.searchParams.get('specifier');
            switch (url.pathname) {
              case '/static':
                return json({ name, described: describe(), id: nextId() });
              case '/lazy': {
                const lazy = await import('./chunks/lazy.mjs');
                return json({ value: lazy.value, id: lazy.nextId() });
              }
              case '/next':
                return json(await attempt(async () => (await import(specifier)).nextId()));
              case '/import':
                return json(await attempt(() => import(specifier).then(() => true)));
              case '/import-nested':
                return json(await attempt(() => importFromNested(specifier).then(() => true)));
            }
            return new Response('not found', { status: 404 });
          },
        };
        """;

    private const string Shared = """
        export const name = 'shared';

        // Module scope exists once per engine; every importer has to see this one.
        let counter = 0;
        export const nextId = () => ++counter;
        """;

    private const string Lazy = """
        import { nextId } from './shared.mjs';

        export const value = 'lazy';
        export { nextId };
        """;

    private const string Describe = """
        import { name } from '../shared.mjs';

        export const describe = () => `described ${name}`;
        export const importFromNested = (specifier) => import(specifier);
        """;

    private static DistFolder CreateBundle()
    {
        var dist = DistFolder.Create()
            .WithServerFile("entry.mjs", Entry)
            .WithServerFile("chunks/shared.mjs", Shared)
            .WithServerFile("chunks/lazy.mjs", Lazy)
            .WithServerFile("chunks/nested/describe.mjs", Describe);

        // Next to the server folder, where a bundle must not reach.
        File.WriteAllText(Path.Combine(dist.DistPath, "outside.mjs"), "export const nextId = () => 'outside';");
        return dist;
    }

    private static Engine CreateEngine(AstroModuleLoader loader) => new(options => options.UseModules(loader));

    [Fact]
    public async Task StaticImports_ResolveTheChunksOfTheBundle()
    {
        using var dist = CreateBundle();
        await using var site = await TestSite.StartAsync(dist, options => options.PoolSize = 1);

        var result = await site.GetJsonAsync("/static");

        Assert.Equal("shared", result.GetProperty("name").GetString());
        Assert.Equal("described shared", result.GetProperty("described").GetString());
        Assert.Equal(1, result.GetProperty("id").GetInt32());
    }

    [Fact]
    public async Task DynamicImports_LoadChunksOnDemand_AndShareModulesWithTheRestOfTheBundle()
    {
        using var dist = CreateBundle();
        await using var site = await TestSite.StartAsync(dist, options => options.PoolSize = 1);
        Assert.Equal(1, (await site.GetJsonAsync("/static")).GetProperty("id").GetInt32());

        var lazy = await site.GetJsonAsync("/lazy");

        Assert.Equal("lazy", lazy.GetProperty("value").GetString());
        Assert.Equal(2, lazy.GetProperty("id").GetInt32());
    }

    [Theory]
    [InlineData("./chunks/shared.mjs")]
    [InlineData("./chunks/./shared.mjs")]
    [InlineData("./chunks/nested/../shared.mjs")]
    [InlineData("./chunks/../chunks/shared.mjs")]
    public async Task Specifiers_AreNormalizedToTheSameModule(string specifier)
    {
        using var dist = CreateBundle();
        await using var site = await TestSite.StartAsync(dist, options => options.PoolSize = 1);
        Assert.Equal(1, (await site.GetJsonAsync("/static")).GetProperty("id").GetInt32());

        var next = await site.GetJsonAsync($"/next?specifier={Uri.EscapeDataString(specifier)}");

        Assert.True(next.GetProperty("ok").GetBoolean(), next.ToString());
        Assert.Equal(2, next.GetProperty("value").GetInt32());
    }

    [Theory]
    [InlineData("/import", "./chunks/missing.mjs", "does not exist in the server bundle")]
    [InlineData("/import", "./chunks", "does not exist in the server bundle")]
    [InlineData("/import", "../outside.mjs", "outside of the server bundle")]
    [InlineData("/import", "./chunks/../../outside.mjs", "outside of the server bundle")]
    [InlineData("/import-nested", "../../../outside.mjs", "outside of the server bundle")]
    [InlineData("/import", "left-pad", "Only relative imports")]
    [InlineData("/import", "node:fs", "Only relative imports")]
    [InlineData("/import", "/etc/hosts", "Only relative imports")]
    [InlineData("/import", "C:/Windows/win.ini", "Only relative imports")]
    [InlineData("/import", "file:///C:/Windows/win.ini", "Only relative imports")]
    [InlineData("/import", "https://example.test/module.mjs", "Only relative imports")]
    [InlineData("/import", ".\\chunks\\shared.mjs", "Only relative imports")]
    [InlineData("/import", "./chunks\\shared.mjs", "file name is not valid")]
    [InlineData("/import", "./chunks/shared.mjs?query", "file name is not valid")]
    [InlineData("/import", "./chunks/shared.mjs::$DATA", "file name is not valid")]
    [InlineData("/import", "./chunks./shared.mjs", "file name is not valid")]
    public async Task ImportsOutsideTheBundle_AreRefused_WithoutRevealingTheHost(string endpoint, string specifier, string reason)
    {
        using var dist = CreateBundle();
        await using var site = await TestSite.StartAsync(dist, options => options.PoolSize = 1);

        var result = await site.GetJsonAsync($"{endpoint}?specifier={Uri.EscapeDataString(specifier)}");

        Assert.False(result.GetProperty("ok").GetBoolean(), result.ToString());
        var error = result.GetProperty("error").GetString()!;
        Assert.Contains(reason, error);
        Assert.DoesNotContain(dist.Root, error);
    }

    [Fact]
    public async Task ImportsOfTheEntry_FromAChunk_ResolveToTheModuleThatIsAlreadyLoaded()
    {
        using var dist = CreateBundle();
        await using var site = await TestSite.StartAsync(dist, options => options.PoolSize = 1);

        var result = await site.GetJsonAsync("/import-nested?specifier=" + Uri.EscapeDataString("../../entry.mjs"));

        Assert.True(result.GetProperty("ok").GetBoolean(), result.ToString());
    }

    [Fact]
    public async Task AChunkThatIsMissing_StopsTheEngine_AndTheLogNamesIt()
    {
        using var dist = DistFolder.Create().WithServerFile(
            "entry.mjs",
            "import './chunks/gone.mjs'; export default { fetch: () => new Response('never') };");
        await using var site = await TestSite.StartAsync(dist);

        using var response = await site.Client.GetAsync("/");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Contains(site.Logs.Entries, entry => entry.Exception?.ToString().Contains("chunks/gone.mjs") == true);
    }

    [Fact]
    public async Task AFileOfTheBundle_IsReadAndParsedOnce_ForAllEngines()
    {
        using var dist = DistFolder.Create()
            .WithServerFile("entry.mjs", "import { value } from './chunks/value.mjs'; export const seen = value;")
            .WithServerFile("chunks/value.mjs", "export const value = 'first';");
        var loader = new AstroModuleLoader(Path.Combine(dist.DistPath, "server", "entry.mjs"));

        using var first = CreateEngine(loader);
        var seenByFirst = (await first.Modules.ImportAsync(loader.Entry)).Get("seen").AsString();
        dist.WithServerFile("chunks/value.mjs", "export const value = 'second';");
        using var second = CreateEngine(loader);
        var seenBySecond = (await second.Modules.ImportAsync(loader.Entry)).Get("seen").AsString();

        Assert.Equal("first", seenByFirst);
        Assert.Equal("first", seenBySecond);
    }

    [Fact]
    public async Task EveryEngine_EvaluatesItsOwnCopyOfAModule()
    {
        using var dist = DistFolder.Create()
            .WithServerFile("entry.mjs", "let count = 0; export const next = () => ++count;");
        var loader = new AstroModuleLoader(Path.Combine(dist.DistPath, "server", "entry.mjs"));

        using var first = CreateEngine(loader);
        using var second = CreateEngine(loader);
        var firstExports = await first.Modules.ImportAsync(loader.Entry);
        var secondExports = await second.Modules.ImportAsync(loader.Entry);

        Assert.Equal(1, first.Invoke(firstExports.Get("next")).AsNumber());
        Assert.Equal(2, first.Invoke(firstExports.Get("next")).AsNumber());
        Assert.Equal(1, second.Invoke(secondExports.Get("next")).AsNumber());
    }

    [Theory]
    [InlineData(null, "./entry.mjs", "entry.mjs")]
    [InlineData("entry.mjs", "./chunks/a.mjs", "chunks/a.mjs")]
    [InlineData("chunks/a.mjs", "./b.mjs", "chunks/b.mjs")]
    [InlineData("chunks/a.mjs", "../entry.mjs", "entry.mjs")]
    [InlineData("chunks/nested/c.mjs", "../../chunks/./b.mjs", "chunks/b.mjs")]
    public void Modules_AreNamedByTheirPathBelowTheFolderOfTheEntry(string? referencing, string specifier, string expectedKey)
    {
        using var dist = DistFolder.Create()
            .WithServerFile("chunks/a.mjs", "export {};")
            .WithServerFile("chunks/b.mjs", "export {};");
        var loader = new AstroModuleLoader(Path.Combine(dist.DistPath, "server", "entry.mjs"));

        var resolved = loader.Resolve(referencing, new ModuleRequest(specifier, []));

        Assert.Equal(expectedKey, resolved.Key);
    }

    [Fact]
    public void Entry_IsTheFileNameOfTheBundle()
    {
        var loader = new AstroModuleLoader(Path.Combine("some", "where", "server.mjs"));

        Assert.Equal("./server.mjs", loader.Entry);
    }
}
