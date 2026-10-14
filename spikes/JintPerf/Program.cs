using System.Diagnostics;
using System.Runtime;
using System.Runtime.InteropServices;
using System.Text;
using Acornima.Ast;
using Jint;
using Jint.Constraints;

// Usage: JintPerf <serverDir> [sanity] [startup] [latency] [throughput] [memory] [limits]   (default: everything)
if (args.Length == 0)
{
    Console.Error.WriteLine("usage: JintPerf <serverDir> [sanity|startup|latency|throughput|memory|limits ...]");
    return 1;
}

var serverDir = Path.GetFullPath(args[0]);
var selected = args.Skip(1).Select(a => a.ToLowerInvariant()).ToHashSet();
bool Wants(string name) => selected.Count == 0 || selected.Contains(name);
var code = await File.ReadAllTextAsync(Path.Combine(serverDir, "entry.mjs"));

Console.WriteLine($"# JintPerf  bundle={Path.GetFileName(Path.GetDirectoryName(serverDir)!)}\\{Path.GetFileName(serverDir)}\\entry.mjs ({code.Length / 1024} KB)");
Console.WriteLine($"{RuntimeInformation.FrameworkDescription}, {RuntimeInformation.OSDescription}, cpus={Environment.ProcessorCount}, serverGC={GCSettings.IsServerGC}");

var prepared = Harness.Prepare(code);
if (!prepared.IsValid)
{
    Console.Error.WriteLine("bundle failed to parse");
    return 2;
}

if (Wants("sanity")) await Benchmarks.Sanity(serverDir, prepared);
if (Wants("startup")) await Benchmarks.Startup(serverDir, code);
if (Wants("latency")) await Benchmarks.Latency(serverDir, prepared);
if (Wants("throughput")) await Benchmarks.Throughput(serverDir, prepared);
if (Wants("memory")) await Benchmarks.Memory(serverDir, code);
if (Wants("limits")) await Benchmarks.Limits(serverDir, prepared);
return 0;

static class Benchmarks
{
    static readonly byte[] Json = Encoding.UTF8.GetBytes("{\"hello\":\"world\"}");

    static string Trim(string s, int max = 110) => s.ReplaceLineEndings(" ") is var t && t.Length > max ? t[..max] + "..." : t;

    static string Describe(Exception ex) => $"{ex.GetType().Name}: {Trim(ex.Message, 90)}";

    public static async Task Sanity(string dir, Prepared<Module> prepared)
    {
        Harness.Banner("B0 sanity (fresh engine, one request each)");
        var (slot, _) = await Harness.CreateSlotAsync(dir, prepared);
        foreach (var (method, path) in new[] { (HttpMethod.Get, "/time"), (HttpMethod.Get, "/items/42?q=x"), (HttpMethod.Get, "/nope"), (HttpMethod.Get, "/crypto"), (HttpMethod.Get, "/boom") })
        {
            try
            {
                var (status, body) = await Harness.SendAsync(slot, method, path);
                Console.WriteLine($"{method} {path} -> {status} {Trim(body)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"{method} {path} -> {Describe(ex)}");
            }
        }

        var echo = await Harness.SendAsync(slot, HttpMethod.Post, "/api/echo", Json, "application/json");
        Console.WriteLine($"POST /api/echo -> {echo.Status} {Trim(echo.Body)}");
        slot.Dispose();
    }

    public static async Task Startup(string dir, string code)
    {
        Harness.Banner("B1 startup (ms; process JIT warms up across rows)");
        for (var i = 1; i <= 4; i++)
        {
            var sw = Stopwatch.StartNew();
            var p = Harness.Prepare(code);
            Console.WriteLine($"PrepareModule #{i}: {sw.Elapsed.TotalMilliseconds,7:N1}  (valid={p.IsValid})");
        }

        Console.WriteLine("-- engines built from source: parse + evaluate per engine");
        await Phase(dir, null);
        Console.WriteLine("-- engines built from one shared Prepared<Module>: evaluate only");
        await Phase(dir, Harness.Prepare(code));

        Console.WriteLine("-- pre-warming a pool of 16 shared-Prepared engines: sequential vs parallel wall time");
        var seq = Stopwatch.StartNew();
        var sequential = new List<Slot>();
        var shared = Harness.Prepare(code);
        for (var i = 0; i < 16; i++)
        {
            sequential.Add((await Harness.CreateSlotAsync(dir, shared)).Slot);
        }

        Console.WriteLine($"sequential x16: {seq.Elapsed.TotalMilliseconds,7:N0} ms");
        sequential.ForEach(s => s.Dispose());

        var par = Stopwatch.StartNew();
        var parallel = await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(async () => (await Harness.CreateSlotAsync(dir, shared)).Slot)));
        Console.WriteLine($"parallel   x16: {par.Elapsed.TotalMilliseconds,7:N0} ms");
        foreach (var s in parallel)
        {
            s.Dispose();
        }

        static async Task Phase(string dir, Prepared<Module>? prepared)
        {
            for (var i = 1; i <= 6; i++)
            {
                var (slot, t) = await Harness.CreateSlotAsync(dir, prepared);
                var sw = Stopwatch.StartNew();
                await Harness.SendAsync(slot, HttpMethod.Get, "/time");
                var first = sw.Elapsed.TotalMilliseconds;
                sw.Restart();
                await Harness.SendAsync(slot, HttpMethod.Get, "/time");
                var second = sw.Elapsed.TotalMilliseconds;
                Console.WriteLine($"engine #{i}: new={t.EngineMs,5:N1} prelude={t.PreludeMs,5:N1} import={t.ImportMs,7:N1} | firstReq={first,6:N1} secondReq={second,5:N1} | ready={t.TotalMs + first,7:N1}");
                slot.Dispose();
            }
        }
    }

    public static async Task Latency(string dir, Prepared<Module> prepared)
    {
        Harness.Banner("B2 warm latency, one engine, sequential, after 300 warm-up requests per scenario");
        var (slot, _) = await Harness.CreateSlotAsync(dir, prepared);
        var scenarios = new (string Name, Func<int, Task<(int Status, string Body)>> Run)[]
        {
            ("GET /time", _ => Harness.SendAsync(slot, HttpMethod.Get, "/time")),
            ("GET /items/{n}?q=x", n => Harness.SendAsync(slot, HttpMethod.Get, $"/items/{n}?q=x")),
            ("POST /api/echo", _ => Harness.SendAsync(slot, HttpMethod.Post, "/api/echo", Json, "application/json")),
            ("GET /nope (404)", _ => Harness.SendAsync(slot, HttpMethod.Get, "/nope")),
            ("GET /crypto", _ => Harness.SendAsync(slot, HttpMethod.Get, "/crypto")),
            ("GET /delay (25ms timer)", _ => Harness.SendAsync(slot, HttpMethod.Get, "/delay")),
        };

        foreach (var (name, run) in scenarios)
        {
            var warm = name.Contains("delay") ? 20 : 300;
            var count = name.Contains("delay") ? 200 : 2000;
            for (var i = 0; i < warm; i++)
            {
                await run(i);
            }

            var samples = new List<double>(count);
            for (var i = 0; i < count; i++)
            {
                var t = Stopwatch.GetTimestamp();
                await run(i);
                samples.Add(Harness.Ms(t, Stopwatch.GetTimestamp()));
            }

            Console.WriteLine($"{name,-24} {Harness.Stats(samples)}");
        }

        slot.Dispose();
    }

    public static async Task Throughput(string dir, Prepared<Module> prepared)
    {
        Harness.Banner("B3 throughput vs pool size (closed loop, GET /items/{n}?q=x{n}, 4 s per cell, response content verified)");
        var maxPool = 24;
        var sw = Stopwatch.StartNew();
        var all = await Task.WhenAll(Enumerable.Range(0, maxPool).Select(_ => Task.Run(async () => (await Harness.CreateSlotAsync(dir, prepared)).Slot)));
        Console.WriteLine($"pre-warmed {maxPool} engines in parallel in {sw.Elapsed.TotalMilliseconds:N0} ms");
        await Task.WhenAll(all.Select(s => Task.Run(async () =>
        {
            for (var i = 0; i < 50; i++)
            {
                await Harness.SendAsync(s, HttpMethod.Get, $"/items/{i}?q=w");
            }
        })));

        Console.WriteLine($"{"pool",4} {"clients",7} {"req/s",9} {"service p50/p95/p99 (ms)",28} {"total p50/p95/p99 (ms)",28} {"bad",4}");
        foreach (var size in new[] { 1, 2, 4, 8, 16, 24 })
        {
            foreach (var factor in new[] { 1, 4 })
            {
                var pool = Pool.From(all.Take(size));
                var r = await ClosedLoop(pool, size * factor, TimeSpan.FromSeconds(4));
                Console.WriteLine($"{size,4} {size * factor,7} {r.Rps,9:N0} {r.Service,28} {r.Total,28} {r.Bad,4}");
            }
        }

        foreach (var s in all)
        {
            s.Dispose();
        }
    }

    static async Task<(double Rps, string Service, string Total, int Bad)> ClosedLoop(Pool pool, int clients, TimeSpan duration)
    {
        var counter = 0;
        var bad = 0;
        var service = new List<double>[clients];
        var total = new List<double>[clients];
        var started = Stopwatch.GetTimestamp();
        var end = started + (long)(duration.TotalSeconds * Stopwatch.Frequency);

        await Task.WhenAll(Enumerable.Range(0, clients).Select(c => Task.Run(async () =>
        {
            var svc = service[c] = new List<double>();
            var tot = total[c] = new List<double>();
            while (Stopwatch.GetTimestamp() < end)
            {
                var t0 = Stopwatch.GetTimestamp();
                var slot = await pool.RentAsync();
                var t1 = Stopwatch.GetTimestamp();
                var id = Interlocked.Increment(ref counter);
                try
                {
                    var (status, body) = await Harness.SendAsync(slot, HttpMethod.Get, $"/items/{id}?q=x{id}");
                    if (status != 200 || !body.Contains($"<h1>Item {id}</h1>") || !body.Contains($"q=x{id}</p>"))
                    {
                        Interlocked.Increment(ref bad);
                    }
                }
                catch
                {
                    Interlocked.Increment(ref bad);
                }
                finally
                {
                    pool.Return(slot);
                }

                var t2 = Stopwatch.GetTimestamp();
                svc.Add(Harness.Ms(t1, t2));
                tot.Add(Harness.Ms(t0, t2));
            }
        })));

        var elapsed = Stopwatch.GetElapsedTime(started).TotalSeconds;
        string Pct(IEnumerable<List<double>> lists)
        {
            var sorted = lists.SelectMany(l => l).OrderBy(x => x).ToArray();
            double P(double q) => sorted[Math.Min(sorted.Length - 1, (int)Math.Ceiling(q * sorted.Length) - 1)];
            return $"{P(0.50):N2} / {P(0.95):N2} / {P(0.99):N2}";
        }

        return (counter / elapsed, Pct(service), Pct(total), bad);
    }

    public static async Task Memory(string dir, string code)
    {
        Harness.Banner("B4 memory (managed heap after forced full GC)");
        var beforeAst = Harness.ManagedMemoryBytes();
        var prepared = Harness.Prepare(code);
        var afterAst = Harness.ManagedMemoryBytes();
        Console.WriteLine($"Prepared<Module> (parsed AST, shared): {(afterAst - beforeAst) / 1048576.0:N1} MB");

        const int n = 8;
        foreach (var shared in new[] { false, true })
        {
            var before = Harness.ManagedMemoryBytes();
            var slots = new List<Slot>();
            for (var i = 0; i < n; i++)
            {
                var (slot, _) = await Harness.CreateSlotAsync(dir, shared ? prepared : null);
                await Harness.SendAsync(slot, HttpMethod.Get, "/time");
                slots.Add(slot);
            }

            var after = Harness.ManagedMemoryBytes();
            Console.WriteLine($"{n} engines, {(shared ? "sharing the Prepared<Module>" : "each parsing the bundle")}: +{(after - before) / 1048576.0:N1} MB total = {(after - before) / n / 1048576.0:N1} MB per engine (after 1 request each)");
            slots.ForEach(s => s.Dispose());
            slots.Clear();
        }

        Console.WriteLine("-- one engine, 20,000 mixed requests (GET /time, GET /items/n, POST /api/echo, GET /nope)");
        var (one, _) = await Harness.CreateSlotAsync(dir, prepared);
        for (var i = 0; i < 300; i++)
        {
            await Harness.SendAsync(one, HttpMethod.Get, $"/items/{i}");
        }

        var baseline = Harness.ManagedMemoryBytes();
        Console.WriteLine($"live heap after warm-up: {baseline / 1048576.0:N1} MB");
        var allocStart = GC.GetTotalAllocatedBytes(precise: true);
        var gen0 = GC.CollectionCount(0);
        var gen2 = GC.CollectionCount(2);
        var window = Stopwatch.StartNew();
        const int total = 20000;
        var windowSamples = new List<double>();
        for (var i = 1; i <= total; i++)
        {
            var t = Stopwatch.GetTimestamp();
            _ = (i % 4) switch
            {
                0 => await Harness.SendAsync(one, HttpMethod.Get, "/time"),
                1 => await Harness.SendAsync(one, HttpMethod.Get, $"/items/{i}?q=x"),
                2 => await Harness.SendAsync(one, HttpMethod.Post, "/api/echo", Json, "application/json"),
                _ => await Harness.SendAsync(one, HttpMethod.Get, "/nope"),
            };
            windowSamples.Add(Harness.Ms(t, Stopwatch.GetTimestamp()));
            if (i % 4000 == 0)
            {
                var allocated = GC.GetTotalAllocatedBytes(precise: true) - allocStart;
                var avgMs = windowSamples.Average();
                windowSamples.Clear();
                Console.WriteLine($"after {i,6} requests: live heap {Harness.ManagedMemoryBytes() / 1048576.0,6:N1} MB | avg {avgMs:N2} ms/req over last window | allocated so far {allocated / 1048576.0:N0} MB ({allocated / i / 1024.0:N0} KB/req)");
            }
        }

        Console.WriteLine($"GC counts during run: gen0={GC.CollectionCount(0) - gen0} gen2={GC.CollectionCount(2) - gen2}; total {window.Elapsed.TotalSeconds:N1} s");
        one.Dispose();
    }

    public static async Task Limits(string dir, Prepared<Module> prepared)
    {
        Harness.Banner("B6 timeouts, cancellation and limits");

        {
            Console.WriteLine("-- 6a: CancellationToken only (no constraint), page burns the CPU for 800 ms; token cancels at 200 ms");
            var (slot, _) = await Harness.CreateSlotAsync(dir, prepared);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            Console.WriteLine("   burn  : " + await Time(async () =>
            {
                var r = await Harness.SendAsync(slot, HttpMethod.Get, "/spin?mode=burn&ms=800", cancellationToken: cts.Token);
                return $"completed with {r.Status}";
            }));
            Console.WriteLine("   time  : " + await Bounded(slot, "/time", TimeSpan.FromSeconds(5)) + "   (right after; a slow reply would mean JS is still burning in the background)");
            slot.Dispose();
        }

        {
            Console.WriteLine("-- 6b: OperationDeadlineConstraint (200 ms) around the request, page spins forever");
            var (slot, _) = await Harness.CreateSlotAsync(dir, prepared);
            Console.WriteLine("   spin  : " + await Bounded(slot, "/spin?mode=spin", TimeSpan.FromMilliseconds(200)));
            Console.WriteLine("   time  : " + await Bounded(slot, "/time", TimeSpan.FromSeconds(5)));
            Console.WriteLine("   spin  : " + await Bounded(slot, "/spin?mode=spin", TimeSpan.FromMilliseconds(200)));
            Console.WriteLine("   items : " + await Bounded(slot, "/items/7?q=again", TimeSpan.FromSeconds(5)));
            await Task.Delay(500);
            Console.WriteLine("   time  : " + await Bounded(slot, "/time", TimeSpan.FromMilliseconds(200)) + "  (after 500 ms idle with a 200 ms budget)");
            slot.Dispose();
        }

        {
            Console.WriteLine("-- 6c: deadline + client abort token (cancel at 150 ms), page spins forever");
            var (slot, _) = await Harness.CreateSlotAsync(dir, prepared);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(150));
            Console.WriteLine("   spin  : " + await Bounded(slot, "/spin?mode=spin", TimeSpan.FromSeconds(30), cts.Token));
            Console.WriteLine("   time  : " + await Bounded(slot, "/time", TimeSpan.FromSeconds(5)));
            slot.Dispose();
        }

        {
            Console.WriteLine("-- 6d: async waits: /delay waits on a 25 ms timer");
            var (slot, _) = await Harness.CreateSlotAsync(dir, prepared);
            Console.WriteLine("   delay, 5 ms budget  : " + await Bounded(slot, "/delay", TimeSpan.FromMilliseconds(5)));
            Console.WriteLine("   time (right after)  : " + await Bounded(slot, "/time", TimeSpan.FromSeconds(5)));
            Console.WriteLine("   delay, 2 s budget   : " + await Bounded(slot, "/delay", TimeSpan.FromSeconds(2)));
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(5));
            Console.WriteLine("   delay, token @5 ms  : " + await Bounded(slot, "/delay", TimeSpan.FromSeconds(30), cts.Token));
            Console.WriteLine("   time (right after)  : " + await Bounded(slot, "/time", TimeSpan.FromSeconds(5)));
            slot.Dispose();
        }

        {
            Console.WriteLine("-- 6e: unbounded recursion in a page (default engine options)");
            var (slot, _) = await Harness.CreateSlotAsync(dir, prepared);
            Console.WriteLine("   recurse : " + await Bounded(slot, "/spin?mode=recurse", TimeSpan.FromSeconds(5)));
            Console.WriteLine("   time    : " + await Bounded(slot, "/time", TimeSpan.FromSeconds(5)));
            slot.Dispose();
        }

        {
            Console.WriteLine("-- 6f: LimitMemory(32 MB) allocation budget");
            var (slot, _) = await Harness.CreateSlotAsync(dir, prepared, o => o.LimitMemory(32 * 1024 * 1024));
            Console.WriteLine("   alloc bomb : " + await Bounded(slot, "/spin?mode=alloc", TimeSpan.FromSeconds(10)));
            Console.WriteLine("   time       : " + await Bounded(slot, "/time", TimeSpan.FromSeconds(5)));
            var memory = slot.Engine.Constraints.Find<MemoryLimitConstraint>();
            var bytes = new List<double>();
            var failures = 0;
            for (var i = 0; i < 300; i++)
            {
                try
                {
                    await Harness.SendAsync(slot, HttpMethod.Get, $"/items/{i}?q=x");
                    if (i >= 50)
                    {
                        bytes.Add((memory?.AllocatedBytes ?? 0) / 1024.0);
                    }
                }
                catch
                {
                    failures++;
                }
            }

            Console.WriteLine($"   300 normal requests: {failures} failures; JS-side allocation per request (KB): {Harness.Stats(bytes).Replace(" ms", " KB")}");
            slot.Dispose();
        }

        {
            Console.WriteLine("-- 6h: overhead of LimitMemory(256 MB) on normal requests (GET /items/n, 3000 each, interleaved x3)");
            foreach (var round in Enumerable.Range(1, 3))
            {
                foreach (var limited in new[] { false, true })
                {
                    var (slot, _) = await Harness.CreateSlotAsync(dir, prepared, limited ? o => o.LimitMemory(256 * 1024 * 1024) : null);
                    for (var i = 0; i < 500; i++)
                    {
                        await Harness.SendAsync(slot, HttpMethod.Get, $"/items/{i}?q=x");
                    }

                    var samples = new List<double>();
                    for (var i = 0; i < 3000; i++)
                    {
                        var t = Stopwatch.GetTimestamp();
                        await Harness.SendAsync(slot, HttpMethod.Get, $"/items/{i}?q=x");
                        samples.Add(Harness.Ms(t, Stopwatch.GetTimestamp()));
                    }

                    Console.WriteLine($"   round {round} {(limited ? "with LimitMemory   " : "without LimitMemory")}: {Harness.Stats(samples)}");
                    slot.Dispose();
                }
            }
        }

        {
            Console.WriteLine("-- 6g: CancellationToken only (no deadline constraint) against an INFINITE loop, 3 s watchdog");
            var (slot, _) = await Harness.CreateSlotAsync(dir, prepared);
            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));
            var attempt = Task.Run(() => Time(async () =>
            {
                var r = await Harness.SendAsync(slot, HttpMethod.Get, "/spin?mode=spin", cancellationToken: cts.Token);
                return $"completed with {r.Status}";
            }));
            if (await Task.WhenAny(attempt, Task.Delay(TimeSpan.FromSeconds(3))) != attempt)
            {
                Console.WriteLine("   still spinning after 3 s: the token alone does NOT interrupt synchronous JS. Exiting the process.");
                Environment.Exit(0);
            }

            Console.WriteLine("   spin  : " + await attempt);
            Dictionary<int, TimeSpan> Snapshot()
            {
                var map = new Dictionary<int, TimeSpan>();
                foreach (ProcessThread thread in Process.GetCurrentProcess().Threads)
                {
                    try
                    {
                        map[thread.Id] = thread.TotalProcessorTime;
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }

                return map;
            }

            for (var window = 1; window <= 3; window++)
            {
                var before = Snapshot();
                await Task.Delay(1000);
                var top = Snapshot()
                    .Select(kv => (kv.Key, Delta: kv.Value - before.GetValueOrDefault(kv.Key)))
                    .OrderByDescending(x => x.Delta)
                    .Take(2)
                    .Select(x => $"thread {x.Key}: {x.Delta.TotalMilliseconds:N0} ms");
                Console.WriteLine($"   idle second {window} after the cancel, busiest threads (a runaway interpreter would stay near 1000 ms): {string.Join(", ", top)}");
            }

            Console.WriteLine("   time  : " + await Bounded(slot, "/time", TimeSpan.FromSeconds(5)));
            slot.Dispose();
        }
    }

    static async Task<string> Bounded(Slot slot, string path, TimeSpan budget, CancellationToken token = default)
    {
        var sw = Stopwatch.StartNew();
        slot.Deadline.Begin(budget, token);
        try
        {
            var (status, body) = await Harness.SendAsync(slot, HttpMethod.Get, path, cancellationToken: token);
            return $"{status} ({body.Length} bytes) in {sw.Elapsed.TotalMilliseconds:N0} ms";
        }
        catch (Exception ex)
        {
            return $"{Describe(ex)} after {sw.Elapsed.TotalMilliseconds:N0} ms";
        }
        finally
        {
            slot.Deadline.End();
        }
    }

    static async Task<string> Time(Func<Task<string>> action)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            return $"{await action()} after {sw.Elapsed.TotalMilliseconds:N0} ms";
        }
        catch (Exception ex)
        {
            return $"{Describe(ex)} after {sw.Elapsed.TotalMilliseconds:N0} ms";
        }
    }
}
