using AstroJint.AspNetCore;
using BenchmarkDotNet.Attributes;
using Microsoft.AspNetCore.Http;

namespace AstroJint.Benchmarks;

/// <summary>A request for a prerendered file, as the static files middleware sees it before the on-demand runtime gets a say.</summary>
public class StaticFilesBenchmarks
{
    private BenchmarkSite _site = null!;
    private AstroStaticFilesMiddleware _middleware = null!;
    private IHeaderDictionary _headers = null!;
    private IHeaderDictionary _conditionalHeaders = null!;
    private string _asset = null!;

    [GlobalSetup]
    public async Task SetupAsync()
    {
        _site = await BenchmarkSite.StartAsync(warmUp: false, withLocals: false);
        _middleware = new AstroStaticFilesMiddleware(static _ => Task.CompletedTask, _site.App);
        _headers = FakeHttp.BrowserHeaders();
        _asset = _site.FindAsset(".css");

        // A browser that revalidates a page it holds sends the ETag it got.
        var page = await RunAsync("/about", _headers);
        _conditionalHeaders = FakeHttp.BrowserHeaders();
        _conditionalHeaders.IfNoneMatch = page.Response.Headers.ETag;

        // A benchmark of a request that failed would be a benchmark of the wrong thing.
        await VerifyAsync("/", _headers, StatusCodes.Status200OK, served: true);
        await VerifyAsync("/about", _headers, StatusCodes.Status200OK, served: true);
        await VerifyAsync(_asset, _headers, StatusCodes.Status200OK, served: true);
        await VerifyAsync("/about", _conditionalHeaders, StatusCodes.Status304NotModified, served: true);
        await VerifyAsync("/time", _headers, StatusCodes.Status200OK, served: false);
    }

    [GlobalCleanup]
    public async Task CleanupAsync() => await _site.DisposeAsync();

    /// <summary>What it costs to make the request, so that it can be subtracted from the rows below.</summary>
    [Benchmark]
    public DefaultHttpContext ContextOnly() => FakeHttp.Create(_site.Services, "GET", "/about", _headers);

    [Benchmark]
    public Task<DefaultHttpContext> Home() => RunAsync("/", _headers);

    [Benchmark]
    public Task<DefaultHttpContext> Page() => RunAsync("/about", _headers);

    [Benchmark]
    public Task<DefaultHttpContext> Asset() => RunAsync(_asset, _headers);

    [Benchmark]
    public Task<DefaultHttpContext> NotModified() => RunAsync("/about", _conditionalHeaders);

    /// <summary>An on-demand page: the middleware finds no file and hands the request on.</summary>
    [Benchmark]
    public Task<DefaultHttpContext> Miss() => RunAsync("/time", _headers);

    private async Task<DefaultHttpContext> RunAsync(string path, IHeaderDictionary headers)
    {
        var context = FakeHttp.Create(_site.Services, "GET", path, headers);
        await _middleware.InvokeAsync(context).ConfigureAwait(false);
        await FakeHttp.StartResponseAsync(context).ConfigureAwait(false);
        return context;
    }

    private async Task VerifyAsync(string path, IHeaderDictionary headers, int status, bool served)
    {
        var context = await RunAsync(path, headers).ConfigureAwait(false);
        var response = context.Response;
        var hasBody = response.ContentLength is > 0;
        if (response.StatusCode != status || (status == StatusCodes.Status200OK && hasBody != served))
        {
            throw new InvalidOperationException($"GET {path} answered {response.StatusCode} (content length {response.ContentLength}); expected {status}, served: {served}.");
        }
    }
}
