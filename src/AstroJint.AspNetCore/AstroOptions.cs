using Microsoft.AspNetCore.Http;

namespace AstroJint.AspNetCore;

/// <summary>Options for hosting a built Astro site (the output of <c>astro build</c> with <c>astro-adapter-jint</c>).</summary>
public sealed class AstroOptions
{
    /// <summary>
    /// Folder that holds <c>astro-jint.json</c>, the <c>client</c> folder and the <c>server</c> folder.
    /// A relative path is resolved against the content root.
    /// </summary>
    public string DistPath { get; set; } = "astro";

    /// <summary>
    /// Number of JavaScript engines, which is also the number of on-demand pages that can render at the same time.
    /// Further requests wait for a free engine. Defaults to the processor count.
    /// </summary>
    public int PoolSize { get; set; } = System.Environment.ProcessorCount;

    /// <summary>
    /// Maximum time from the start of an on-demand request until its response is ready, including the wait for a
    /// free engine. Use <see cref="Timeout.InfiniteTimeSpan"/> to disable. Defaults to 30 seconds.
    /// </summary>
    public TimeSpan RequestTimeout { get; set; } = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Upper bound, in bytes, for what JavaScript may allocate while it renders one request.
    /// <see langword="null"/> disables the limit. Defaults to 128 MiB.
    /// </summary>
    public long? MemoryLimitBytes { get; set; } = 128L * 1024 * 1024;

    /// <summary>Time zone that JavaScript's <c>Date</c> uses. Defaults to UTC rather than the machine's zone.</summary>
    public TimeZoneInfo TimeZone { get; set; } = TimeZoneInfo.Utc;

    /// <summary>
    /// Create every engine in the background when the host starts, so the first on-demand request does not pay for it.
    /// Static pages are served immediately either way.
    /// </summary>
    public bool WarmUp { get; set; } = true;

    /// <summary>Largest request body that is forwarded to Astro, in bytes. Larger bodies get a 413. Defaults to 10 MiB.</summary>
    public long MaxRequestBodyBytes { get; set; } = 10L * 1024 * 1024;

    /// <summary>
    /// Produces the value of <c>Astro.locals</c> for a request. The result is serialized to JSON with web defaults
    /// (camelCase), so it has to be plain data.
    /// </summary>
    public Func<HttpContext, object?>? LocalsFactory { get; set; }

    /// <summary>Values for the variables declared in the Astro config's <c>env.schema</c> (<c>astro:env</c>).</summary>
    public IDictionary<string, string?> Env { get; } = new Dictionary<string, string?>(StringComparer.Ordinal);

    /// <summary>
    /// Decides which URLs JavaScript's <c>fetch</c> may request, for example the origin of a remote image that the
    /// <c>/_image</c> endpoint downloads. <see langword="null"/> (the default) rejects every request, so that code
    /// running in the engine cannot reach the network or the internal services of this machine by accident.
    /// </summary>
    public Func<Uri, bool>? FetchUrlFilter { get; set; }

    /// <summary>
    /// How long session data (<c>Astro.session</c>) is kept after it was last read or written. The data lives in the
    /// <see cref="Microsoft.Extensions.Caching.Distributed.IDistributedCache"/> of the host, which is an in-memory
    /// cache unless the application registers another one. Use <see cref="Timeout.InfiniteTimeSpan"/> to keep the
    /// data until Astro deletes it. Defaults to 20 minutes, like ASP.NET Core's own session.
    /// </summary>
    public TimeSpan SessionIdleTimeout { get; set; } = TimeSpan.FromMinutes(20);
}
