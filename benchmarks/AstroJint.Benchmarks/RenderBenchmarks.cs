using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Http;

namespace AstroJint.Benchmarks;

/// <summary>
/// On-demand pages, rendered by Astro inside Jint through the whole host: request translation, an engine of the
/// pool, Astro's router and renderer, response translation. One engine, one request at a time.
/// </summary>
public class RenderBenchmarks
{
    private BenchmarkSite _site = null!;
    private IHeaderDictionary _headers = null!;
    private IHeaderDictionary _postHeaders = null!;
    private MemoryStream _postBody = null!;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        _site = await BenchmarkSite.StartAsync(poolSize: 1, warmUp: true, withLocals: true);
        _headers = FakeHttp.BrowserHeaders();

        var body = Encoding.UTF8.GetBytes("{\"message\":\"" + new string('x', 1000) + "\"}");
        _postBody = new MemoryStream(body);
        _postHeaders = FakeHttp.BrowserHeaders();
        _postHeaders.ContentType = "application/json";
        _postHeaders.ContentLength = body.Length;

        // A benchmark of a request that failed would be a benchmark of the wrong thing.
        await VerifyAsync("/api/time", StatusCodes.Status200OK);
        await VerifyAsync("/time", StatusCodes.Status200OK);
        await VerifyAsync("/products/1", StatusCodes.Status200OK);
        await VerifyAsync("/products/unknown", StatusCodes.Status404NotFound);
        var echo = await PostAsync().ConfigureAwait(false);
        if (echo.Response.StatusCode != StatusCodes.Status200OK)
        {
            throw new InvalidOperationException($"POST /api/echo answered {echo.Response.StatusCode}.");
        }
    }

    [GlobalCleanup]
    public async Task CleanupAsync()
    {
        await _site.DisposeAsync();
        await _postBody.DisposeAsync();
    }

    /// <summary>The smallest response: the cost of a request that has almost nothing to render.</summary>
    [Benchmark]
    public Task<DefaultHttpContext> ApiTime() => GetAsync("/api/time");

    [Benchmark]
    public Task<DefaultHttpContext> PageTime() => GetAsync("/time");

    [Benchmark]
    public Task<DefaultHttpContext> PageProduct() => GetAsync("/products/1");

    /// <summary>The page returns a 404, which Astro answers with its prerendered 404 page.</summary>
    [Benchmark]
    public Task<DefaultHttpContext> PageNotFound() => GetAsync("/products/unknown");

    [Benchmark]
    public Task<DefaultHttpContext> PostEcho() => PostAsync();

    private async Task<DefaultHttpContext> GetAsync(string path)
    {
        var context = FakeHttp.Create(_site.Services, "GET", path, _headers);
        await _site.App.RenderAsync(context).ConfigureAwait(false);
        return context;
    }

    private async Task<DefaultHttpContext> PostAsync()
    {
        _postBody.Position = 0;
        var context = FakeHttp.Create(_site.Services, "POST", "/api/echo", _postHeaders, _postBody);
        await _site.App.RenderAsync(context).ConfigureAwait(false);
        return context;
    }

    private async Task VerifyAsync(string path, int status)
    {
        var context = await GetAsync(path).ConfigureAwait(false);
        if (context.Response.StatusCode != status)
        {
            throw new InvalidOperationException($"GET {path} answered {context.Response.StatusCode}; expected {status}.");
        }
    }
}
