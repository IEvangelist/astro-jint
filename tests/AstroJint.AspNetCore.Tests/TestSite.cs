using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AstroJint.AspNetCore.Tests;

/// <summary>A temporary folder with the layout that <c>astro build</c> produces with astro-adapter-jint.</summary>
internal sealed class DistFolder : IDisposable
{
    private DistFolder(string root) => Root = root;

    /// <summary>The content root of the host; it contains <see cref="DistPath"/> as <c>dist</c>.</summary>
    public string Root { get; }

    public string DistPath => Path.Combine(Root, "dist");

    public string ClientPath => Path.Combine(DistPath, "client");

    public static DistFolder Create(
        string baseFolder = "/",
        string trailingSlash = "ignore",
        string buildFormat = "directory",
        string? entryFile = null)
    {
        var root = Path.Combine(Path.GetTempPath(), "astro-jint-tests", Guid.NewGuid().ToString("N"));
        var dist = Path.Combine(root, "dist");
        Directory.CreateDirectory(Path.Combine(dist, "client"));
        Directory.CreateDirectory(Path.Combine(dist, "server"));

        File.Copy(entryFile ?? Path.Combine(AppContext.BaseDirectory, "Fixtures", "entry.mjs"), Path.Combine(dist, "server", "entry.mjs"));
        File.WriteAllText(
            Path.Combine(dist, "astro-jint.json"),
            JsonSerializer.Serialize(new
            {
                version = 1,
                astroVersion = "test",
                @base = baseFolder,
                trailingSlash,
                buildFormat,
                assetsDir = "_astro",
                client = "client",
                server = "server",
                entry = "server/entry.mjs",
                routes = Array.Empty<object>(),
            }));

        return new DistFolder(root);
    }

    /// <summary>A typical set of prerendered files.</summary>
    public static DistFolder CreateDefault() => Create()
        .WithClientFile("index.html", "<h1>Home</h1>")
        .WithClientFile("about/index.html", "<h1>About</h1>")
        .WithClientFile("404.html", "<h1>Prerendered 404</h1>")
        .WithClientFile("_astro/app.abc123.js", "console.log('app');")
        .WithClientFile("favicon.svg", "<svg xmlns=\"http://www.w3.org/2000/svg\"/>")
        .WithClientFile("data.json", "{\"a\":1}")
        .WithClientFile("robots.txt", "User-agent: *")
        .WithClientFile(".well-known/security.txt", "Contact: mailto:security@example.test")
        .WithClientFile("private/.env", "TOKEN=1")
        .WithClientFile("blob.bin", Enumerable.Range(0, 1000).Select(i => (byte)i).ToArray());

    public DistFolder WithClientFile(string relativePath, string content) =>
        WithClientFile(relativePath, System.Text.Encoding.UTF8.GetBytes(content));

    public DistFolder WithClientFile(string relativePath, byte[] content)
    {
        var path = Path.Combine(ClientPath, relativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllBytes(path, content);
        return this;
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(Root, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}

internal sealed record LogEntry(LogLevel Level, string Category, string Message, Exception? Exception);

internal sealed class ListLoggerProvider : ILoggerProvider
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

/// <summary>An ASP.NET Core host on the in-memory test server that serves a <see cref="DistFolder"/>.</summary>
internal sealed class TestSite : IAsyncDisposable
{
    private readonly WebApplication _app;
    private readonly DistFolder? _ownedDist;

    private TestSite(WebApplication app, DistFolder? ownedDist, ListLoggerProvider logs)
    {
        _app = app;
        _ownedDist = ownedDist;
        Logs = logs;
        Client = app.GetTestClient();
    }

    public HttpClient Client { get; }

    public ListLoggerProvider Logs { get; }

    public IServiceProvider Services => _app.Services;

    internal AstroApp Astro => _app.Services.GetRequiredService<AstroApp>();

    /// <param name="dist">The build to host. Defaults to <see cref="DistFolder.CreateDefault"/>, which the site then owns.</param>
    /// <param name="configure">Adjusts the options; by default the pool has two engines and no warm-up.</param>
    /// <param name="configureApp">Adds middleware and endpoints; runs before <c>UseAstro()</c>.</param>
    public static async Task<TestSite> StartAsync(
        DistFolder? dist = null,
        Action<AstroOptions>? configure = null,
        Action<WebApplication>? configureApp = null,
        Action<IServiceCollection>? configureServices = null)
    {
        var ownedDist = dist is null ? DistFolder.CreateDefault() : null;
        dist ??= ownedDist!;

        var logs = new ListLoggerProvider();
        var builder = WebApplication.CreateBuilder(new WebApplicationOptions
        {
            ContentRootPath = dist.Root,
            EnvironmentName = Environments.Development,
        });

        builder.WebHost.UseTestServer();
        builder.Logging.ClearProviders();
        builder.Logging.AddProvider(logs);
        builder.Services.AddAstro(options =>
        {
            options.DistPath = "dist";
            options.PoolSize = 2;
            options.WarmUp = false;
            configure?.Invoke(options);
        });
        configureServices?.Invoke(builder.Services);

        var app = builder.Build();
        configureApp?.Invoke(app);
        app.UseAstro();
        await app.StartAsync();

        return new TestSite(app, ownedDist, logs);
    }

    public async Task<JsonElement> GetJsonAsync(string path)
    {
        using var response = await Client.GetAsync(path);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.Clone();
    }

    /// <summary>Sets the client address that the test server leaves empty.</summary>
    public static Action<WebApplication> WithRemoteAddress(string address) => app =>
        app.Use((context, next) =>
        {
            context.Connection.RemoteIpAddress = IPAddress.Parse(address);
            return next(context);
        });

    public async ValueTask DisposeAsync()
    {
        Client.Dispose();
        await _app.StopAsync();
        await _app.DisposeAsync();
        _ownedDist?.Dispose();
    }
}
