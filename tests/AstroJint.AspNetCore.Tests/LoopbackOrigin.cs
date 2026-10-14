using System.Collections.Concurrent;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AstroJint.AspNetCore.Tests;

/// <summary>A server on a real loopback port, so that the <c>fetch</c> of an engine has somewhere to go.</summary>
internal sealed class LoopbackOrigin : IAsyncDisposable
{
    public static readonly byte[] Bytes = [1, 2, 3, 250, 251, 252];

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

    public static async Task<LoopbackOrigin> StartAsync()
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
        app.MapGet("/hello", () => Results.Text("hello from the origin"));
        app.MapGet("/bytes", () => Results.Bytes(Bytes, "application/octet-stream"));
        app.MapGet("/redirect", () => Results.Redirect("/hello"));
        app.MapGet("/redirect-twice", () => Results.Redirect("/redirect"));
        app.MapGet("/missing", () => Results.NotFound());
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
