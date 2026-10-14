using System.Collections.Concurrent;
using AstroJint.AspNetCore;
using AstroJint.Conformance;

[assembly: AssemblyFixture(typeof(SiteHost))]

namespace AstroJint.Conformance;

/// <summary>
/// Starts each fixture once per test run and shares it between the test classes, like the <c>before()</c> hooks of the
/// upstream tests share one build and one server. A test that needs other options passes a <c>variant</c> name.
/// </summary>
public sealed class SiteHost : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<Task<ConformanceSite>>> _sites = new(StringComparer.Ordinal);

    public Task<ConformanceSite> GetAsync(string fixture, string variant = "", Action<AstroOptions>? configure = null) =>
        _sites.GetOrAdd(
            $"{fixture}|{variant}",
            _ => new Lazy<Task<ConformanceSite>>(() => ConformanceSite.StartAsync(fixture, configure))).Value;

    public async ValueTask DisposeAsync()
    {
        foreach (var site in _sites.Values)
        {
            if (site.IsValueCreated && site.Value.IsCompletedSuccessfully)
            {
                await site.Value.Result.DisposeAsync();
            }
        }
    }
}
