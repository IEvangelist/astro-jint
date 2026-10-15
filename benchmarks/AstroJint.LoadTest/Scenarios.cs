using System.Text;
using System.Text.RegularExpressions;

namespace AstroJint.LoadTest;

/// <summary>A request that is sent as it is, and the status that a healthy server answers it with.</summary>
internal sealed record LoadRequest(string Description, byte[] Bytes, int ExpectedStatus);

/// <summary>What the generator sends. Every scenario is a list of requests that each connection walks through, over and over.</summary>
internal static partial class Scenarios
{
    // The headers of a browser: the server pays for each of them, so they belong in the measurement.
    private const string BrowserHeaders =
        "Connection: keep-alive\r\n" +
        "sec-ch-ua: \"Chromium\";v=\"131\", \"Not_A Brand\";v=\"24\"\r\n" +
        "sec-ch-ua-mobile: ?0\r\n" +
        "sec-ch-ua-platform: \"Windows\"\r\n" +
        "Upgrade-Insecure-Requests: 1\r\n" +
        "User-Agent: Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0.0.0 Safari/537.36\r\n" +
        "Accept: text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,image/apng,*/*;q=0.8\r\n" +
        "Sec-Fetch-Site: none\r\n" +
        "Sec-Fetch-Mode: navigate\r\n" +
        "Sec-Fetch-User: ?1\r\n" +
        "Sec-Fetch-Dest: document\r\n" +
        "Accept-Encoding: gzip, deflate, br, zstd\r\n" +
        "Accept-Language: en-US,en;q=0.9\r\n" +
        "Cookie: theme=dark; astro-session=0123456789abcdef\r\n";

    private static readonly string[] s_names =
    [
        "static-page",
        "static-home",
        "static-asset",
        "not-modified",
        "ondemand-time",
        "ondemand-product",
        "ondemand-post",
        "mixed",
    ];

    public static IReadOnlyList<string> Names => s_names;

    public static async Task<LoadRequest[]> BuildAsync(string name, Uri baseUrl)
    {
        switch (name)
        {
            case "static-page":
                return [Get(baseUrl, "/about")];
            case "static-home":
                return [Get(baseUrl, "/")];
            case "static-asset":
                return [Get(baseUrl, await FindStylesheetAsync(baseUrl).ConfigureAwait(false))];
            case "not-modified":
                return [await GetNotModifiedAsync(baseUrl, "/about").ConfigureAwait(false)];
            case "ondemand-time":
                return [Get(baseUrl, "/time")];
            case "ondemand-product":
                return [Get(baseUrl, "/products/1")];
            case "ondemand-post":
                return [PostEcho(baseUrl)];
            case "mixed":
                // Seven requests for prerendered files to three for pages that are rendered on demand, the way a hybrid site is used.
                var stylesheet = await FindStylesheetAsync(baseUrl).ConfigureAwait(false);
                return
                [
                    Get(baseUrl, "/"),
                    Get(baseUrl, "/time"),
                    Get(baseUrl, "/about"),
                    Get(baseUrl, stylesheet),
                    Get(baseUrl, "/blog/hello-jint"),
                    Get(baseUrl, "/products/1"),
                    await GetNotModifiedAsync(baseUrl, "/about").ConfigureAwait(false),
                    Get(baseUrl, "/blog/engine-pool"),
                    PostEcho(baseUrl),
                    Get(baseUrl, "/blog/static-meets-dynamic"),
                ];
            default:
                throw new ArgumentException($"Unknown scenario '{name}'. Choose one of: {string.Join(", ", s_names)}, or all.");
        }
    }

    private static LoadRequest Get(Uri baseUrl, string path, string? ifNoneMatch = null, int expectedStatus = 200)
    {
        var request = new StringBuilder()
            .Append("GET ").Append(path).Append(" HTTP/1.1\r\n")
            .Append("Host: ").Append(baseUrl.Authority).Append("\r\n")
            .Append(BrowserHeaders);
        if (ifNoneMatch is not null)
        {
            request.Append("If-None-Match: ").Append(ifNoneMatch).Append("\r\n");
        }

        request.Append("\r\n");
        var description = ifNoneMatch is null ? "GET " + path : "GET " + path + " (revalidated)";
        return new LoadRequest(description, Encoding.ASCII.GetBytes(request.ToString()), expectedStatus);
    }

    private static LoadRequest PostEcho(Uri baseUrl)
    {
        var body = "{\"message\":\"" + new string('x', 1000) + "\"}";
        var request = new StringBuilder()
            .Append("POST /api/echo?x=1 HTTP/1.1\r\n")
            .Append("Host: ").Append(baseUrl.Authority).Append("\r\n")
            .Append(BrowserHeaders)
            .Append("Content-Type: application/json\r\n")
            .Append("Content-Length: ").Append(Encoding.UTF8.GetByteCount(body)).Append("\r\n\r\n")
            .Append(body);
        return new LoadRequest("POST /api/echo", Encoding.UTF8.GetBytes(request.ToString()), 200);
    }

    /// <summary>A browser that holds the page asks whether it is still current, with the ETag it received.</summary>
    private static async Task<LoadRequest> GetNotModifiedAsync(Uri baseUrl, string path)
    {
        using var http = new HttpClient { BaseAddress = baseUrl };
        using var response = await http.GetAsync(path).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
        var etag = response.Headers.ETag?.ToString() ?? throw new InvalidOperationException($"GET {path} sent no ETag.");
        return Get(baseUrl, path, etag, expectedStatus: 304);
    }

    /// <summary>A file of the build with a hashed name, found where a browser finds it: in the home page.</summary>
    private static async Task<string> FindStylesheetAsync(Uri baseUrl)
    {
        using var http = new HttpClient { BaseAddress = baseUrl };
        var html = await http.GetStringAsync("/").ConfigureAwait(false);
        var match = StylesheetPattern().Match(html);
        return match.Success ? match.Value : throw new InvalidOperationException("The home page links no stylesheet from /_astro/.");
    }

    [GeneratedRegex("""/_astro/[^"'\s>]+\.css""")]
    private static partial Regex StylesheetPattern();
}
