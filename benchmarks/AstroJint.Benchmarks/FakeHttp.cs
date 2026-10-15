using System.Net;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;

namespace AstroJint.Benchmarks;

/// <summary>
/// Requests for the in-process benchmarks. They behave like Kestrel where the host depends on it: the
/// <c>OnStarting</c> callbacks of a response run once the pipeline is done, which a bare <see cref="DefaultHttpContext"/> never does.
/// </summary>
internal static class FakeHttp
{
    /// <summary>The headers a browser sends with a page request.</summary>
    public static IHeaderDictionary BrowserHeaders(string host = "localhost:5000") => new HeaderDictionary
    {
        ["Host"] = host,
        ["Connection"] = "keep-alive",
        ["sec-ch-ua"] = "\"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\"",
        ["sec-ch-ua-mobile"] = "?0",
        ["sec-ch-ua-platform"] = "\"Windows\"",
        ["Upgrade-Insecure-Requests"] = "1",
        ["User-Agent"] = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36",
        ["Accept"] = "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8",
        ["Sec-Fetch-Site"] = "none",
        ["Sec-Fetch-Mode"] = "navigate",
        ["Sec-Fetch-User"] = "?1",
        ["Sec-Fetch-Dest"] = "document",
        ["Accept-Encoding"] = "gzip, deflate, br, zstd",
        ["Accept-Language"] = "en-US,en;q=0.9",
        ["Cookie"] = "theme=dark; astro-session=0123456789abcdef",
    };

    public static DefaultHttpContext Create(IServiceProvider services, string method, string path, IHeaderDictionary headers, Stream? body = null, string query = "")
    {
        var features = new FeatureCollection();
        features.Set<IHttpRequestFeature>(new HttpRequestFeature
        {
            Method = method,
            Scheme = "http",
            Path = path,
            RawTarget = path + query,
            QueryString = query,
            Headers = headers,
            Body = body ?? Stream.Null,
        });
        features.Set<IHttpResponseFeature>(new StartingResponseFeature());
        features.Set<IHttpResponseBodyFeature>(new StreamResponseBodyFeature(Stream.Null));
        features.Set<IHttpConnectionFeature>(new HttpConnectionFeature { RemoteIpAddress = IPAddress.Loopback, LocalPort = 5000 });
        return new DefaultHttpContext(features) { RequestServices = services };
    }

    /// <summary>Runs what a server runs before it sends the headers.</summary>
    public static Task StartResponseAsync(HttpContext context) =>
        context.Features.Get<IHttpResponseFeature>() is StartingResponseFeature feature ? feature.StartAsync() : Task.CompletedTask;

    private sealed class StartingResponseFeature : HttpResponseFeature
    {
        private List<(Func<object, Task> Callback, object State)>? _starting;

        public override void OnStarting(Func<object, Task> callback, object state) => (_starting ??= []).Add((callback, state));

        public async Task StartAsync()
        {
            if (_starting is null)
            {
                return;
            }

            for (var i = _starting.Count - 1; i >= 0; i--)
            {
                var (callback, state) = _starting[i];
                await callback(state).ConfigureAwait(false);
            }
        }
    }
}
