using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// What the tests of <c>sessions/*</c> need around the <c>app-sessions</c> fixture. Upstream drives the <c>AstroSession</c> class
/// with a mock cookie object and a mock unstorage; here the class is the one behind <c>Astro.session</c>, the storage is the
/// <see cref="IDistributedCache"/> of the host (a <see cref="SessionSpyCache"/> that a test can seed and query) and the cookies are
/// the <c>Cookie</c> and <c>Set-Cookie</c> headers. The endpoint <c>/api/ops</c> of the fixture runs the calls of a test on
/// <c>Astro.session</c> of one request and reports what each returned or threw.
/// </summary>
internal static class AppSessions
{
    public const string Fixture = "app-sessions";

    /// <summary>
    /// A host whose <see cref="IDistributedCache"/> is a new <see cref="SessionSpyCache"/>. The host (and so the cache) is shared by
    /// every call with the same <paramref name="variant"/> and <paramref name="config"/>, so each class names a variant of its own.
    /// </summary>
    public static async Task<(ConformanceSite Site, SessionSpyCache Cache)> GetSessionSiteAsync(
        this SiteHost sites, string variant, object? config = null)
    {
        var site = await sites.GetAsync(
            Fixture,
            config,
            variant: variant,
            configureServices: services => services.AddSingleton<IDistributedCache>(new SessionSpyCache()));
        return (site, (SessionSpyCache)site.Services.GetRequiredService<IDistributedCache>());
    }

    /// <summary>The id of a session that the cookie of a test names: a version 4 UUID, which is all that Astro accepts from a cookie.</summary>
    public static string NewId() => Guid.NewGuid().ToString();

    /// <summary>The value of the <c>Cookie</c> header that carries the session id.</summary>
    public static string CookieFor(string name, string id) => $"{name}={id}";

    /// <summary>
    /// Makes one request to <c>/api/ops</c>, which runs <paramref name="steps"/> (see <see cref="SessionOp"/>) on <c>Astro.session</c> in
    /// order. The session is persisted before the response is returned, so the cache and the response show the end state.
    /// </summary>
    public static async Task<SessionRun> RunSessionAsync(this ConformanceSite site, string? cookie, params object[] steps)
    {
        using var content = new StringContent(JsonSerializer.Serialize(new { steps }), Encoding.UTF8, "application/json");
        (string Name, string Value)[] headers = cookie is null ? [] : [("Cookie", cookie)];
        using var response = await site.SendAsync(HttpMethod.Post, "/api/ops", content, headers);
        var body = await response.Content.ReadAsStringAsync();

        var results = new List<SessionStep>();
        if (response.StatusCode == HttpStatusCode.OK)
        {
            using var document = JsonDocument.Parse(body);
            results.AddRange(document.RootElement.GetProperty("results").EnumerateArray().Select(ParseStep));
        }

        return new SessionRun((int)response.StatusCode, body, response.HeaderValues("Set-Cookie"), results);
    }

    /// <summary>Whether <c>Astro.session</c> is defined on the endpoint <c>/api/has-session</c>.</summary>
    public static async Task<bool> HasSessionAsync(this ConformanceSite site)
    {
        using var response = await site.GetAsync("/api/has-session");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.GetProperty("hasSession").GetBoolean();
    }

    /// <summary>Whether any script of the server build contains <paramref name="text"/>.</summary>
    public static bool ServerBundleContains(this ConformanceSite site, string text) =>
        Directory.EnumerateFiles(Path.Combine(site.DistPath, "server"), "*.mjs", SearchOption.AllDirectories)
            .Any(file => File.ReadAllText(file).Contains(text, StringComparison.Ordinal));

    private static SessionStep ParseStep(JsonElement result)
    {
        if (!result.GetProperty("ok").GetBoolean())
        {
            return new SessionStep(false, false, default, result.GetProperty("error").GetString());
        }

        var undefined = result.GetProperty("isUndefined").GetBoolean();
        return new SessionStep(true, undefined, undefined ? default : result.GetProperty("value").Clone(), null);
    }
}

/// <summary>The steps of a request to <c>/api/ops</c>: the calls that the tests of <c>AstroSession</c> make on the class itself.</summary>
internal static class SessionOp
{
    public static object Set(string key, object? value) => new { op = "set", key, value };

    /// <summary>A <c>set()</c> with <c>{ fun: function () {} }</c>, which devalue cannot serialize.</summary>
    public static object SetFunction(string key) => new { op = "setFunction", key };

    public static object Get(string key) => new { op = "get", key };

    public static object Has(string key) => new { op = "has", key };

    public static object Delete(string key) => new { op = "delete", key };

    public static object Keys() => new { op = "keys" };

    public static object Regenerate() => new { op = "regenerate" };

    public static object Destroy() => new { op = "destroy" };

    /// <summary>The <c>sessionID</c> of the session, which is undefined until the session has an id.</summary>
    public static object Id() => new { op = "id" };
}

/// <summary>What one step returned (<see cref="Value"/>, undefined for <c>undefined</c>) or the message of the error that it threw.</summary>
internal sealed record SessionStep(bool Ok, bool IsUndefined, JsonElement Value, string? Error)
{
    /// <summary>The value as JSON, or the word <c>undefined</c>; a string is quoted.</summary>
    public string Json => !Ok ? $"threw: {Error}" : IsUndefined ? "undefined" : Value.GetRawText();

    /// <summary>The value of a step that returned a string.</summary>
    public string? Text => Value.GetString();
}

/// <summary>The response of <c>/api/ops</c>.</summary>
internal sealed record SessionRun(int Status, string Body, IReadOnlyList<string> SetCookies, IReadOnlyList<SessionStep> Steps)
{
    /// <summary>The step at <paramref name="index"/>, after checking that the request completed and the step did not throw.</summary>
    public SessionStep Step(int index)
    {
        EnsureCompleted();
        var step = Steps[index];
        if (!step.Ok)
        {
            Assert.Fail($"Step {index} threw: {step.Error}");
        }

        return step;
    }

    /// <summary>The message of the error that the step at <paramref name="index"/> threw, after checking that the request completed.</summary>
    public string Error(int index)
    {
        EnsureCompleted();
        var step = Steps[index];
        if (step.Ok)
        {
            Assert.Fail($"Step {index} returned {step.Json} instead of throwing.");
        }

        return step.Error ?? string.Empty;
    }

    private void EnsureCompleted()
    {
        if (Status != 200)
        {
            Assert.Fail($"The request answered {Status} instead of 200: {Body}");
        }
    }

    /// <summary>The value of the cookie that the response sets with the given name (<c>deleted</c> for a deleted one), or null.</summary>
    public string? CookieValue(string name)
    {
        var header = SetCookie(name);
        return header?.Split(';', 2)[0][(name.Length + 1)..];
    }

    /// <summary>The attributes of that cookie (<c>Path=/</c>, <c>HttpOnly</c>, ...), without its name and value.</summary>
    public IReadOnlyList<string> CookieAttributes(string name) =>
        SetCookie(name) is { } header ? [.. header.Split(';').Skip(1).Select(attribute => attribute.Trim())] : [];

    private string? SetCookie(string name) =>
        SetCookies.SingleOrDefault(header => header.StartsWith(name + "=", StringComparison.Ordinal));
}

internal enum SessionCallKind
{
    Get,
    Set,
    Remove,
}

/// <summary>A call that the host made on the cache for the session with <paramref name="Id"/>.</summary>
internal readonly record struct SessionCall(SessionCallKind Kind, string Id);

/// <summary>
/// The <see cref="IDistributedCache"/> of a host under test, standing in for the mock unstorage of upstream: a test seeds the data
/// of a session (<see cref="Seed"/>), reads what the host wrote (<see cref="Stored"/>), looks at the calls it made
/// (<see cref="Mark"/> and <see cref="CallsSince"/>) and can make writes of one session fail. Entries are kept by the id that the
/// cookie carries; the key prefix of the host is stripped.
/// </summary>
internal sealed class SessionSpyCache : IDistributedCache
{
    private const string KeyPrefix = "astro-jint:session:";

    private readonly ConcurrentDictionary<string, byte[]> _entries = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, string> _writeFailures = new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<SessionCall> _calls = new();

    /// <summary>Stores the data of a session without counting as a call. <paramref name="data"/> is what Astro writes (devalue).</summary>
    public void Seed(string id, string data) => _entries[id] = Encoding.UTF8.GetBytes(data);

    /// <summary>The data stored for the session, or null when there is none.</summary>
    public string? Stored(string id) => _entries.TryGetValue(id, out var data) ? Encoding.UTF8.GetString(data) : null;

    /// <summary>Makes every write for the session fail with an <see cref="InvalidOperationException"/> that has this message.</summary>
    public void FailWrites(string id, string message) => _writeFailures[id] = message;

    /// <summary>The number of calls so far, to pass to <see cref="CallsSince"/> after a request.</summary>
    public int Mark() => _calls.Count;

    public IReadOnlyList<SessionCall> CallsSince(int mark) => [.. _calls.Skip(mark)];

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        var id = Record(SessionCallKind.Get, key);
        return Task.FromResult<byte[]?>(_entries.TryGetValue(id, out var data) ? data : null);
    }

    public Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        var id = Record(SessionCallKind.Set, key);
        if (_writeFailures.TryGetValue(id, out var failure))
        {
            return Task.FromException(new InvalidOperationException(failure));
        }

        _entries[id] = value;
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        var id = Record(SessionCallKind.Remove, key);
        _entries.TryRemove(id, out _);
        return Task.CompletedTask;
    }

    public Task RefreshAsync(string key, CancellationToken token = default) => Task.CompletedTask;

    public byte[]? Get(string key) => throw Synchronous();

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) => throw Synchronous();

    public void Remove(string key) => throw Synchronous();

    public void Refresh(string key) => throw Synchronous();

    private static NotSupportedException Synchronous() => new("The session store of the host only calls the asynchronous methods.");

    private string Record(SessionCallKind kind, string key)
    {
        if (!key.StartsWith(KeyPrefix, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"The key '{key}' does not start with '{KeyPrefix}'.");
        }

        var id = key[KeyPrefix.Length..];
        _calls.Enqueue(new SessionCall(kind, id));
        return id;
    }
}

/// <summary>
/// Data of a session as Astro stores it, written out by hand in the flat form of devalue (<c>stringify(new Map([...]))</c>, with
/// the entries <c>{ data, expires }</c> that Astro 7.3.8 writes; <c>expires</c> is -1 for an entry that expired).
/// </summary>
internal static class SessionSeeds
{
    /// <summary><c>new Map([['key', { data: 'value' }]])</c></summary>
    public const string Value = """[["Map",1,2],"key",{"data":3},"value"]""";

    /// <summary><c>new Map([['key', { data: 'value', expires: -1 }]])</c></summary>
    public const string Expired = """[["Map",1,2],"key",{"data":3,"expires":4},"value",-1]""";

    /// <summary>The entries keep, delete and update, as <c>{ data }</c>.</summary>
    public const string Sparse =
        """[["Map",1,2,4,5,7,8],"keep",{"data":3},"original","delete",{"data":6},"remove","update",{"data":9},"old"]""";

    /// <summary><c>new Map([['key', 'value']])</c>: the value without the <c>{ data }</c> wrapper.</summary>
    public const string RawValue = """[["Map",1,2],"key","value"]""";

    /// <summary><c>new Map([['key', 'old']])</c></summary>
    public const string RawOld = """[["Map",1,2],"key","old"]""";

    /// <summary><c>new Map([['token', { data: 'secret' }]])</c></summary>
    public const string Token = """[["Map",1,2],"token",{"data":3},"secret"]""";

    /// <summary><c>new Map([['user', { data: 'alice' }]])</c></summary>
    public const string User = """[["Map",1,2],"user",{"data":3},"alice"]""";

    /// <summary><c>new Map()</c></summary>
    public const string EmptyMap = """[["Map"]]""";

    /// <summary><c>{ notAMap: true }</c></summary>
    public const string NotAMap = """[{"notAMap":1},true]""";

    /// <summary>JSON that devalue turns into something that is not a Map.</summary>
    public const string NumberArray = "[1,2,3]";

    /// <summary>Not JSON at all.</summary>
    public const string Corrupt = "invalid-json";
}
