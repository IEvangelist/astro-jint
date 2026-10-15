using AstroJint.AspNetCore;
using BenchmarkDotNet.Attributes;

namespace AstroJint.Benchmarks;

/// <summary>Maps a request path to a prerendered file, which every request does before anything else.</summary>
public class ResolverBenchmarks
{
    private AstroStaticFileResolver _resolver = null!;

    [ParamsSource(nameof(Paths))]
    public string RequestPath { get; set; } = "/";

    public static IEnumerable<string> Paths
    {
        get
        {
            var dist = BenchmarkSite.FindDistPath();
            return
            [
                "/",
                "/about",
                "/about/",
                "/blog/hello-jint",
                BenchmarkSite.FindAsset(dist, ".css"),
                "/favicon.svg",
                "/time",
                "/products/1",
            ];
        }
    }

    [GlobalSetup]
    public void Setup()
    {
        var dist = BenchmarkSite.FindDistPath();
        var manifest = AstroManifest.Load(dist);
        _resolver = AstroStaticFileResolver.FromDirectory(Path.GetFullPath(manifest.Client, dist), manifest);
    }

    [Benchmark]
    public bool Resolve() => _resolver.Resolve(RequestPath).Kind != StaticFileMatchKind.None;
}
