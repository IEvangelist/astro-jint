using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AstroJint.Conformance;

/// <summary>
/// A server on a real loopback port. The upstream tests fetch pictures from unsplash; the sandboxed engines here can only
/// reach what the host allows, and a test run must not depend on the internet, so a test serves what it needs from here.
/// </summary>
internal sealed class LoopbackOrigin : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly ConcurrentQueue<string> _requests;

    private LoopbackOrigin(WebApplication app, Uri address, ConcurrentQueue<string> requests)
    {
        _app = app;
        Address = address;
        _requests = requests;
    }

    public Uri Address { get; }

    /// <summary>The path and query of every request so far, in order.</summary>
    public IReadOnlyList<string> Requests => [.. _requests];

    public Uri Url(string pathAndQuery) => new(Address, pathAndQuery);

    public static async Task<LoopbackOrigin> StartAsync(Action<WebApplication> map)
    {
        var requests = new ConcurrentQueue<string>();
        var builder = WebApplication.CreateSlimBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();

        var app = builder.Build();
        app.Use((context, next) =>
        {
            requests.Enqueue(context.Request.Path + context.Request.QueryString.ToUriComponent());
            return next(context);
        });
        map(app);
        await app.StartAsync();

        var address = app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
        return new LoopbackOrigin(app, new Uri(address), requests);
    }

    public async ValueTask DisposeAsync()
    {
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}
