using System.Diagnostics;
using System.Net.Http.Headers;
using System.Threading.Channels;
using Acornima.Ast;
using Jint;
using Jint.Constraints;
using Jint.Native;

/// <summary>One engine plus the host-owned deadline constraint registered on it.</summary>
sealed class Slot(Engine engine, OperationDeadlineConstraint deadline) : IDisposable
{
    public Engine Engine { get; } = engine;
    public OperationDeadlineConstraint Deadline { get; } = deadline;
    public int Served { get; set; }
    public void Dispose() => Engine.Dispose();
}

readonly record struct StartupTimings(double EngineMs, double PreludeMs, double ImportMs)
{
    public double TotalMs => EngineMs + PreludeMs + ImportMs;
}

static class Harness
{
    // Verified in spike S2: makes Error.stack start with "Name: message" and use \n.
    public const string Prelude = """
        (() => {
          const original = Object.getOwnPropertyDescriptor(Error.prototype, 'stack');
          if (!original || !original.get) return;
          Object.defineProperty(Error.prototype, 'stack', {
            configurable: true,
            enumerable: false,
            get() {
              const frames = String(original.get.call(this)).replace(/\r\n/g, '\n');
              return Error.prototype.toString.call(this) + '\n' + frames;
            },
            set(value) {
              Object.defineProperty(this, 'stack', { value, writable: true, configurable: true, enumerable: false });
            },
          });
        })();
        """;

    public static Prepared<Module> Prepare(string code) => Engine.PrepareModule(code, "entry.mjs");

    public static async Task<(Slot Slot, StartupTimings Timings)> CreateSlotAsync(
        string serverDir,
        Prepared<Module>? prepared = null,
        Action<Options>? configure = null)
    {
        var deadline = new OperationDeadlineConstraint();

        var sw = Stopwatch.StartNew();
        var engine = new Engine(options =>
        {
            options.UseWebApis();
            options.UseFetch(f => f.UrlFilter = _ => false);
            options.UseModules(serverDir);
            options.TimeZone = TimeZoneInfo.Utc;
            options.AddConstraint(deadline);
            configure?.Invoke(options);
        });
        var engineMs = sw.Elapsed.TotalMilliseconds;

        sw.Restart();
        engine.Execute(Prelude);
        var preludeMs = sw.Elapsed.TotalMilliseconds;

        sw.Restart();
        Jint.Native.Object.ObjectInstance ns;
        if (prepared is { } p)
        {
            engine.Modules.Add("entry", builder => builder.AddModule(p));
            ns = await engine.Modules.ImportAsync("entry");
        }
        else
        {
            ns = await engine.Modules.ImportAsync("./entry.mjs");
        }

        engine.WebApi.SetFetchHandler(ns.Get("default"));
        var importMs = sw.Elapsed.TotalMilliseconds;

        return (new Slot(engine, deadline), new StartupTimings(engineMs, preludeMs, importMs));
    }

    public static async Task<(int Status, string Body)> SendAsync(
        Slot slot,
        HttpMethod method,
        string path,
        byte[]? body = null,
        string? contentType = null,
        CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(method, "http://localhost" + path);
        request.Headers.TryAddWithoutValidation("User-Agent", "jint-perf/1.0");
        if (body is not null)
        {
            request.Content = new ByteArrayContent(body);
            if (contentType is not null)
            {
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
            }
        }

        using var response = await slot.Engine.WebApi.InvokeFetchHandlerAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(CancellationToken.None);
        slot.Served++;
        return ((int)response.StatusCode, text);
    }

    public static string Stats(IReadOnlyList<double> samplesMs)
    {
        if (samplesMs.Count == 0)
        {
            return "n=0";
        }

        var sorted = samplesMs.OrderBy(x => x).ToArray();
        double P(double q) => sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(q * sorted.Length) - 1)];
        return $"n={sorted.Length,6} min={sorted[0],6:N2} p50={P(0.50),6:N2} p95={P(0.95),6:N2} p99={P(0.99),6:N2} max={sorted[^1],7:N2} mean={sorted.Average(),6:N2} ms";
    }

    public static double Ms(long startTimestamp, long endTimestamp) => Stopwatch.GetElapsedTime(startTimestamp, endTimestamp).TotalMilliseconds;

    public static long ManagedMemoryBytes()
    {
        GC.Collect();
        GC.WaitForPendingFinalizers();
        return GC.GetTotalMemory(forceFullCollection: true);
    }

    public static void Banner(string text) => Console.WriteLine($"\n## {text}");
}

/// <summary>Trivial blocking pool of engines (closed set, no replacement).</summary>
sealed class Pool
{
    private readonly Channel<Slot> _channel;

    private Pool(int size) => _channel = Channel.CreateBounded<Slot>(size);

    public int Size { get; private init; }

    public static async Task<Pool> CreateAsync(int size, Func<Task<Slot>> factory, bool parallel)
    {
        var pool = new Pool(size) { Size = size };
        Slot[] slots;
        if (parallel)
        {
            slots = await Task.WhenAll(Enumerable.Range(0, size).Select(_ => Task.Run(factory)));
        }
        else
        {
            slots = new Slot[size];
            for (var i = 0; i < size; i++)
            {
                slots[i] = await factory();
            }
        }

        foreach (var slot in slots)
        {
            pool._channel.Writer.TryWrite(slot);
        }

        return pool;
    }

    public static Pool From(IEnumerable<Slot> slots)
    {
        var list = slots.ToList();
        var pool = new Pool(list.Count) { Size = list.Count };
        list.ForEach(s => pool._channel.Writer.TryWrite(s));
        return pool;
    }

    public ValueTask<Slot> RentAsync() => _channel.Reader.ReadAsync();

    public void Return(Slot slot) => _channel.Writer.TryWrite(slot);

    public IEnumerable<Slot> DrainAll()
    {
        while (_channel.Reader.TryRead(out var slot))
        {
            yield return slot;
        }
    }
}
