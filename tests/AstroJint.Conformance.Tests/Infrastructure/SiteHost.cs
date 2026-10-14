using System.Collections.Concurrent;
using System.Text.Json;
using AstroJint.AspNetCore;
using AstroJint.Conformance;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

[assembly: AssemblyFixture(typeof(SiteHost))]

namespace AstroJint.Conformance;

/// <summary>
/// Starts each fixture once per test run and shares it between the test classes, like the <c>before()</c> hooks of the
/// upstream tests share one build and one server. A test that needs other host options passes a <c>variant</c> name.
/// </summary>
public sealed class SiteHost : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, Lazy<Task<ConformanceSite>>> _sites = new(StringComparer.Ordinal);

    /// <param name="fixture">Folder name below <c>tests/conformance/fixtures</c>.</param>
    /// <param name="config">Changes to the Astro config for the build, for example <c>new { output = "server", @base = "/docs" }</c>.</param>
    /// <param name="configure">Adjusts the options of the host; name the <paramref name="variant"/> when it does.</param>
    /// <param name="variant">Tells hosts of one build with different host options apart.</param>
    /// <param name="configureApp">Adds middleware before the Astro pipeline; name the <paramref name="variant"/> when it does.</param>
    /// <param name="configureServices">Registers services; name the <paramref name="variant"/> when it does.</param>
    public Task<ConformanceSite> GetAsync(
        string fixture,
        object? config = null,
        Action<AstroOptions>? configure = null,
        string variant = "",
        Action<WebApplication>? configureApp = null,
        Action<IServiceCollection>? configureServices = null) =>
        _sites.GetOrAdd(
            $"{fixture}|{Serialize(config)}|{variant}",
            _ => new Lazy<Task<ConformanceSite>>(() => ConformanceSite.StartAsync(fixture, config, configure, configureApp, configureServices))).Value;

    internal static string Serialize(object? config) => config switch
    {
        null => "{}",
        string => throw new ArgumentException("The Astro config changes are an object, not a string; name the variant with 'variant:'.", nameof(config)),
        _ => JsonSerializer.Serialize(config),
    };

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
