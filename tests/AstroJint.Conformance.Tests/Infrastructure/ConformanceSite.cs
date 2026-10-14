using System.Collections.Concurrent;
using System.Net;
using AngleSharp.Dom;
using AngleSharp.Html.Parser;
using AstroJint.AspNetCore;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AstroJint.Conformance;

/// <summary>
/// A built Astro fixture, hosted by ASP.NET Core on Kestrel with a loopback socket, so requests take the same route as
/// in production. The client never follows redirects or keeps cookies; tests look at both and set what they need.
/// </summary>
public sealed class ConformanceSite : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly ListLoggerProvider _logs;

    private ConformanceSite(WebApplication app, ListLoggerProvider logs, string fixture)
    {
        _app = app;
        _logs = logs;
        Fixture = fixture;
        BaseAddress = new Uri(app.Urls.Single());
        Client = new HttpClient(new SocketsHttpHandler
        {
            AllowAutoRedirect = false,
            UseCookies = false,
            AutomaticDecompression = DecompressionMethods.None,
        })
        {
            BaseAddress = BaseAddress,
            Timeout = TimeSpan.FromSeconds(60),
        };
    }

    public string Fixture { get; }

    public Uri BaseAddress { get; }

    public HttpClient Client { get; }

    public IServiceProvider Services => _app.Services;

    public IReadOnlyList<LogEntry> Logs => _logs.Entries;

    /// <summary>Builds the fixture when needed, then starts a host for it. The pool has two engines unless configured.</summary>
    /// <param name="fixture">Folder name below <c>tests/conformance/fixtures</c>.</param>
    /// <param name="config">Changes to the Astro config of the fixture, like the inline config an upstream test passes to <c>loadFixture()</c>.</param>
    /// <param name="configure">Adjusts the options of the host.</param>
    /// <param name="configureApp">Adds middleware and endpoints before the Astro pipeline.</param>
    public static async Task<ConformanceSite> StartAsync(
        string fixture,
        object? config = null,
        Action<AstroOptions>? configure = null,
        Action<WebApplication>? configureApp = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var dist = await FixtureBuilder.EnsureBuiltAsync(fixture, SiteHost.Serialize(config));

        var logs = new ListLoggerProvider();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = Path.GetDirectoryName(dist),
            EnvironmentName = Environments.Production,
        });

        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        builder.Services.AddAstro(options =>
        {
            options.DistPath = dist;
            options.PoolSize = 2;
            options.WarmUp = false;
            configure?.Invoke(options);
        });
        configureServices?.Invoke(builder.Services);

        var app = builder.Build();
        configureApp?.Invoke(app);
        app.UseAstro();
        await app.StartAsync();

        return new ConformanceSite(app, logs, fixture);
    }

    public Task<HttpResponseMessage> GetAsync(string path) => Client.GetAsync(path);

    public async Task<string> GetStringAsync(string path)
    {
        using var response = await Client.GetAsync(path);
        return await response.Content.ReadAsStringAsync();
    }

    /// <summary>Requests the page and parses its HTML, whatever the status.</summary>
    public async Task<IDocument> GetHtmlAsync(string path)
    {
        using var response = await Client.GetAsync(path);
        return ParseHtml(await response.Content.ReadAsStringAsync());
    }

    public static IDocument ParseHtml(string html) => new HtmlParser().ParseDocument(html);

    public Task<HttpResponseMessage> SendAsync(HttpMethod method, string path, HttpContent? content = null, params (string Name, string Value)[] headers)
    {
        var request = new HttpRequestMessage(method, path) { Content = content };
        foreach (var (name, value) in headers)
        {
            if (!request.Headers.TryAddWithoutValidation(name, value))
            {
                request.Content ??= new ByteArrayContent([]);
                request.Content.Headers.TryAddWithoutValidation(name, value);
            }
        }

        return Client.SendAsync(request);
    }

    /// <summary>
    /// Sends the request over a plain socket, so the request target reaches the host exactly as written; <see cref="HttpClient"/>
    /// would normalise backslashes, invalid escapes and dot segments.
    /// </summary>
    /// <param name="target">The request target, such as <c>/a/../b?c=d</c>.</param>
    public Task<RawResponse> SendRawAsync(string target, string method = "GET", byte[]? body = null, params (string Name, string Value)[] headers) =>
        RawHttp.SendAsync(BaseAddress, RawHttp.BuildRequest(BaseAddress, method, target, headers, body));

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
    }
}

public sealed record LogEntry(LogLevel Level, string Category, string Message, Exception? Exception);

public sealed class ListLoggerProvider : ILoggerProvider
{
    private readonly ConcurrentQueue<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => [.. _entries];

    public ILogger CreateLogger(string categoryName) => new ListLogger(categoryName, _entries);

    public void Dispose()
    {
    }

    private sealed class ListLogger(string category, ConcurrentQueue<LogEntry> entries) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            entries.Enqueue(new LogEntry(logLevel, category, formatter(state, exception), exception));
    }
}
