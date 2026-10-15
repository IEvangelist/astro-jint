using System.Globalization;
using System.Net;

namespace AstroJint.AspNetCore.Tests;

/// <summary>
/// A file that is held in memory has to be answered exactly like one that is read from disk, which ASP.NET Core's
/// file result does: the same status, the same headers and the same body, whatever the conditional and range
/// headers of the request say. One site serves a build from memory, the other one from disk, and every request is
/// sent to both.
/// </summary>
public sealed class AstroStaticFilesCacheDifferentialTests : IAsyncLifetime
{
    private static readonly string[] Paths =
    [
        "/",
        "/about",
        "/about/",
        "/index.html",
        "/_astro/app.abc123.js",
        "/robots.txt",
        "/data.json",
        "/blob.bin",
        "/favicon.svg",
        "/empty.txt",
        "/big.bin",
        "/.well-known/security.txt",
    ];

    private static readonly HttpMethod[] Methods = [HttpMethod.Get, HttpMethod.Head];

    private DistFolder? _dist;
    private TestSite? _disk;
    private TestSite? _memory;

    private TestSite Disk => _disk ?? throw new InvalidOperationException("The site was not started.");

    private TestSite Memory => _memory ?? throw new InvalidOperationException("The site was not started.");

    public async ValueTask InitializeAsync()
    {
        _dist = DistFolder.CreateDefault()
            .WithClientFile("empty.txt", "")
            .WithClientFile("big.bin", new byte[(int)AstroStaticFileCache.MaxFileBytes + 1])
            .WithStaticHeaders("index.html", ("Content-Security-Policy", "default-src 'self'"), ("X-Build", "test"));

        // Distinct times in the past, each with a fraction of a second that the validators must not carry.
        var modified = DateTime.UtcNow.AddHours(-3);
        var offset = 0;
        foreach (var file in Directory.EnumerateFiles(_dist.ClientPath, "*", SearchOption.AllDirectories).Order(StringComparer.Ordinal))
        {
            File.SetLastWriteTimeUtc(file, modified.AddMinutes(-offset++).AddTicks(1_234_567));
        }

        _disk = await TestSite.StartAsync(_dist, options => options.StaticFileCacheBytes = 0);
        _memory = await TestSite.StartAsync(_dist);
    }

    public async ValueTask DisposeAsync()
    {
        if (_memory is not null)
        {
            await _memory.DisposeAsync();
        }

        if (_disk is not null)
        {
            await _disk.DisposeAsync();
        }

        _dist?.Dispose();
    }

    [Fact]
    public void OnlyTheSiteThatIsMeantToHoldsFilesInMemory()
    {
        Assert.Equal(0, Disk.Astro.FileCache.Count);

        // The files of the default build and the empty one; the big one is above the limit of a file.
        Assert.Equal(10, Memory.Astro.FileCache.Count);
        Assert.False(Memory.Astro.FileCache.TryGet("big.bin", out _));
    }

    [Fact]
    public async Task EveryRequest_IsAnsweredLikeAFileFromDisk()
    {
        var mismatches = new List<string>();
        var statuses = new HashSet<HttpStatusCode>();
        var compared = 0;

        foreach (var path in Paths)
        {
            var plain = await SendAsync(Disk.Client, HttpMethod.Get, path, []);
            Assert.Equal(HttpStatusCode.OK, plain.Status);
            var etag = plain.Header("etag") ?? throw new InvalidOperationException($"{path} has no ETag.");
            var lastModified = plain.Header("last-modified") ?? throw new InvalidOperationException($"{path} has no Last-Modified.");

            foreach (var (name, headers) in Cases(etag, lastModified))
            {
                foreach (var method in Methods)
                {
                    var expected = await SendAsync(Disk.Client, method, path, headers);
                    var actual = await SendAsync(Memory.Client, method, path, headers);

                    compared++;
                    statuses.Add(expected.Status);
                    if (!expected.SameAs(actual))
                    {
                        mismatches.Add($"{method} {path} ({name}){Environment.NewLine}  disk:   {expected}{Environment.NewLine}  memory: {actual}");
                    }
                }
            }
        }

        Assert.True(mismatches.Count == 0, $"{mismatches.Count} of {compared} requests were answered differently:{Environment.NewLine}{string.Join(Environment.NewLine, mismatches.Take(15))}");
        Assert.True(compared > 5000, $"Only {compared} requests were compared.");
        Assert.Superset(
            new HashSet<HttpStatusCode> { HttpStatusCode.OK, HttpStatusCode.PartialContent, HttpStatusCode.NotModified, HttpStatusCode.PreconditionFailed, HttpStatusCode.RequestedRangeNotSatisfiable },
            statuses);
    }

    [Fact]
    public async Task CachedFiles_AreServedWithoutTheFileSystem()
    {
        using var first = await Memory.Client.GetAsync("/about");
        var etag = first.Headers.ETag!.ToString();

        foreach (var file in Directory.EnumerateFiles(_dist!.ClientPath, "*", SearchOption.AllDirectories))
        {
            File.Delete(file);
        }

        using var whole = await Memory.Client.GetAsync("/about");
        Assert.Equal(HttpStatusCode.OK, whole.StatusCode);
        Assert.Equal("<h1>About</h1>", await whole.Content.ReadAsStringAsync());

        using var revalidation = await Memory.Client.SendAsync(WithHeader("/about", "If-None-Match", etag));
        Assert.Equal(HttpStatusCode.NotModified, revalidation.StatusCode);

        using var range = await Memory.Client.SendAsync(WithHeader("/about", "Range", "bytes=1-3"));
        Assert.Equal(HttpStatusCode.PartialContent, range.StatusCode);
        Assert.Equal("h1>", await range.Content.ReadAsStringAsync());

        using var head = await Memory.Client.SendAsync(new HttpRequestMessage(HttpMethod.Head, "/about"));
        Assert.Equal(HttpStatusCode.OK, head.StatusCode);
        Assert.Equal(14, head.Content.Headers.ContentLength);

        // The file that is too big to be held in memory is read from disk, where it no longer is: the engine answers.
        using var big = await Memory.Client.GetAsync("/big.bin");
        Assert.Equal(HttpStatusCode.NotFound, big.StatusCode);
    }

    private static HttpRequestMessage WithHeader(string path, string name, string value)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        Assert.True(request.Headers.TryAddWithoutValidation(name, value));
        return request;
    }

    private static async Task<Snapshot> SendAsync(HttpClient client, HttpMethod method, string path, (string Name, string Value)[] headers)
    {
        using var request = new HttpRequestMessage(method, path);
        foreach (var (name, value) in headers)
        {
            Assert.True(request.Headers.TryAddWithoutValidation(name, value), $"{name} cannot be sent.");
        }

        using var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsByteArrayAsync();
        var lines = response.Headers
            .Concat(response.Content.Headers)
            .Select(header => $"{header.Key.ToLowerInvariant()}: {string.Join(", ", header.Value)}")
            .Where(line => !line.StartsWith("date: ", StringComparison.Ordinal) && !line.StartsWith("server: ", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();

        return new Snapshot(response.StatusCode, lines, body);
    }

    // Every kind of precondition and range, alone and combined, with values that match, that do not match, and that make no sense.
    private static List<Case> Cases(string etag, string lastModified)
    {
        var modified = DateTimeOffset.ParseExact(lastModified, "r", CultureInfo.InvariantCulture);
        var tags = new (string Name, string Value)[]
        {
            ("exact", etag),
            ("weak", "W/" + etag),
            ("star", "*"),
            ("list", "\"another\", " + etag),
            ("other", "\"another\""),
            ("garbage", "not-an-etag"),
        };
        var dates = new (string Name, string Value)[]
        {
            ("exact", lastModified),
            ("older", HttpDate(modified.AddSeconds(-1))),
            ("newer", HttpDate(modified.AddMinutes(30))),
            ("future", HttpDate(DateTimeOffset.UtcNow.AddDays(2))),
            ("rfc850", modified.UtcDateTime.ToString("dddd, dd-MMM-yy HH:mm:ss 'GMT'", CultureInfo.InvariantCulture)),
            ("garbage", "yesterday"),
        };
        string[] tagHeaders = ["If-None-Match", "If-Match"];
        string[] dateHeaders = ["If-Modified-Since", "If-Unmodified-Since"];

        List<Case> cases =
        [
            new("plain", []),
            new("If-None-Match twice", [("If-None-Match", etag), ("If-None-Match", etag)]),
            new("If-None-Match empty", [("If-None-Match", "")]),
        ];

        foreach (var header in tagHeaders)
        {
            foreach (var (name, value) in tags)
            {
                cases.Add(new($"{header}: {name}", [(header, value)]));
            }
        }

        foreach (var header in dateHeaders)
        {
            foreach (var (name, value) in dates)
            {
                cases.Add(new($"{header}: {name}", [(header, value)]));
            }
        }

        foreach (var tagHeader in tagHeaders)
        {
            foreach (var (tagName, tag) in tags)
            {
                foreach (var dateHeader in dateHeaders)
                {
                    foreach (var (dateName, date) in dates)
                    {
                        cases.Add(new($"{tagHeader}: {tagName}, {dateHeader}: {dateName}", [(tagHeader, tag), (dateHeader, date)]));
                    }
                }
            }
        }

        foreach (var (noneMatchName, noneMatch) in tags)
        {
            foreach (var (matchName, match) in tags)
            {
                cases.Add(new($"If-None-Match: {noneMatchName}, If-Match: {matchName}", [("If-None-Match", noneMatch), ("If-Match", match)]));
            }
        }

        foreach (var (modifiedName, modifiedSince) in dates)
        {
            foreach (var (unmodifiedName, unmodifiedSince) in dates)
            {
                cases.Add(new($"If-Modified-Since: {modifiedName}, If-Unmodified-Since: {unmodifiedName}", [("If-Modified-Since", modifiedSince), ("If-Unmodified-Since", unmodifiedSince)]));
            }
        }

        string[] ranges = ["bytes=0-4", "bytes=5-", "bytes=-3", "bytes=999999-", "bytes=0-1,3-4", "bytes=abc", "items=0-4", "bytes=0-0", "bytes=4-2", "bytes=0-99999999"];
        Case[] rangeConditions =
        [
            new("without conditions", []),
            new("If-None-Match: exact", [("If-None-Match", etag)]),
            new("If-None-Match: other", [("If-None-Match", "\"another\"")]),
            new("If-Match: exact", [("If-Match", etag)]),
            new("If-Match: other", [("If-Match", "\"another\"")]),
            new("If-Modified-Since: exact", [("If-Modified-Since", lastModified)]),
            new("If-Modified-Since: older", [("If-Modified-Since", HttpDate(modified.AddSeconds(-1)))]),
            new("If-Unmodified-Since: older", [("If-Unmodified-Since", HttpDate(modified.AddSeconds(-1)))]),
            new("If-Unmodified-Since: newer", [("If-Unmodified-Since", HttpDate(modified.AddMinutes(30)))]),
            new("If-Range: ETag", [("If-Range", etag)]),
            new("If-Range: other ETag", [("If-Range", "\"another\"")]),
            new("If-Range: weak ETag", [("If-Range", "W/" + etag)]),
            new("If-Range: date", [("If-Range", lastModified)]),
            new("If-Range: older date", [("If-Range", HttpDate(modified.AddSeconds(-1)))]),
            new("If-Range: newer date", [("If-Range", HttpDate(modified.AddMinutes(30)))]),
            new("If-Range: garbage", [("If-Range", "yesterday")]),
        ];

        foreach (var range in ranges)
        {
            foreach (var condition in rangeConditions)
            {
                cases.Add(new($"Range: {range}, {condition.Name}", [("Range", range), .. condition.Headers]));
            }
        }

        foreach (var condition in rangeConditions.Where(condition => condition.Headers.Length > 0 && condition.Headers[0].Name == "If-Range"))
        {
            cases.Add(new($"{condition.Name}, without a range", condition.Headers));
        }

        return cases;
    }

    private static string HttpDate(DateTimeOffset value) => value.UtcDateTime.ToString("r", CultureInfo.InvariantCulture);

    private sealed record Case(string Name, (string Name, string Value)[] Headers);

    private sealed class Snapshot(HttpStatusCode status, string[] headers, byte[] body)
    {
        public HttpStatusCode Status { get; } = status;

        public string[] Headers { get; } = headers;

        public byte[] Body { get; } = body;

        public string? Header(string name) =>
            Headers.FirstOrDefault(line => line.StartsWith(name + ": ", StringComparison.Ordinal)) is { } line ? line[(name.Length + 2)..] : null;

        public bool SameAs(Snapshot other) =>
            Status == other.Status && Headers.AsSpan().SequenceEqual(other.Headers) && Body.AsSpan().SequenceEqual(other.Body);

        public override string ToString() => $"{(int)Status} [{string.Join(" | ", Headers)}] {Body.Length} bytes";
    }
}
