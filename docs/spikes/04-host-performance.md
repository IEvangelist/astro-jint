# Spike S4: speed and efficiency of the C# host

**Question.** The C# side of the adapter should add as little as possible to a request. Where
does a request spend its time, what can be made cheaper, and what cannot?

**Short answer.**

- A **prerendered page** no longer touches the disk or the JavaScript runtime. It is served from
  memory in about 0.35 microseconds of adapter code and 0.6 KB of garbage, 285 to 360 times faster
  than before in-process, and 7 times more requests per second through real Kestrel (4.5-5.7K to 29-34K).
- An **on-demand page** costs about 0.25 to 0.9 ms of Jint executing Astro. The C# side (request
  translation, engine pool, response write) is an estimated 4-5 microseconds of that, so there is
  little left to win there. The way to make a page faster is to prerender it, which is what a hybrid
  site is for.
- The request and response bridge now allocates the same few KB for a 2 KB and for a 200 KB page
  (a 200 KB response used to allocate 404 KB on the large object heap).
- The memory budget per request (`MemoryLimitBytes`, on by default) costs at most a few percent,
  within the noise of the measurement.
- Server GC is required for a pool of more than a handful of engines (5 times the throughput of
  workstation GC at 64 connections). The other runtime settings that were tried (concurrent GC, GC
  heap count and affinity, gen0 budget, pool size, TieredPGO) are in section 4; none beat the
  defaults beyond the noise, so the defaults stay.

## Method

Machine: Intel i7-14700K (8 P-cores and 12 E-cores, 28 logical processors), 32 GB, Windows 11,
.NET SDK 10.0.402 with runtime 10.0.13, Jint `5.0.0-preview-2249`, Astro 7.3.8. The sample site is
`samples/hybrid`. Details of both tools: [benchmarks/README.md](../../benchmarks/README.md).

- **BenchmarkDotNet** (`AstroJint.Benchmarks`) for one operation at a time, in process, with the
  memory diagnoser. The requests are made on a `DefaultHttpContext`, so Kestrel's own costs are not in
  those numbers. Default job (not `--quick`) for every number below.
- **Load test** (`AstroJint.LoadTest`) for the whole host: the sample host in Release with Server GC,
  64 keep-alive connections over loopback, raw sockets with the headers of a browser, 3 to 10 seconds of
  warm-up and 10 to 15 seconds of measurement, every status checked.
- Both run on the machine that also runs the host, with other programs open. **Run-to-run noise of
  the on-demand scenarios is about +-30 percent** (hybrid P/E cores, background work, GC timing), so
  on-demand comparisons were made interleaved, repeated, and with medians. The static changes are far
  outside that noise.

## Where a request spends its time

| Path | Cost | Who pays |
| --- | ---: | --- |
| Static file, from memory (after) | ~0.35 us, 0.6 KB | Adapter: path resolution, header writes, copy |
| Static file, from disk (before) | 155-213 us, 4.4-4.8 KB | A file open and a file stream per request |
| Make the `HttpContext` of a benchmark | ~0.2 us, 1.1 KB | The benchmark itself (subtracted above) |
| Request bridge (GET / POST) | 0.6 / 0.8 us, 0.9 / 2.2 KB | Adapter |
| Response bridge | ~0.5 us, 0.75 KB, any body size | Adapter |
| On-demand API route (`/api/time`) | 239 us, 127 KB | Jint running Astro's router and runtime |
| On-demand page (`/time`, `/products/1`) | 0.8-0.9 ms, 296 KB | Jint running Astro's renderer |
| On-demand 404 page | 0.8 ms, 236 KB | Jint |

So on-demand requests are dominated by the JavaScript interpreter, and so is their garbage: a page
allocates about 300 KB per request, and the adapter's own share of that is a few KB. Nothing the
C# side does changes that, and no Jint engine option makes the interpreter itself faster (the exact
constraints such as the memory limit run before every statement, the amortized ones every 64).

Under load the same request costs about three times the CPU time that it does on a single engine
in a benchmark (about 2.7 ms against 0.9 ms for `/time`). The garbage keeps the collector busy
(section 4), the 28 threads share caches and memory bandwidth, and 12 of the 20 cores are slower E-cores.

## 1. Prerendered files: served from memory

Before, every request for a prerendered file went through ASP.NET Core's static file middleware:
resolve the path, open the file, build a `PhysicalFileInfo` and a stream, copy it, close it. Now
`AstroStaticFiles` keeps the files up to 1 MiB in one immutable snapshot (default budget 32 MiB,
`StaticFileCacheBytes = 0` goes back to the disk) with the headers, ETag and `Last-Modified`
computed once. The two answers that make up almost all traffic, the whole file and a plain
`If-None-Match` revalidation (`304`), are written directly (`HEAD` too); a `Range` request or any
other precondition is handed to ASP.NET Core's file result, which implements those rules.

BenchmarkDotNet, `StaticFilesBenchmarks`, same machine, `Cache = False` is the disk path:

| Request | Disk | Memory | Allocated (disk, memory) | Faster |
| --- | ---: | ---: | ---: | ---: |
| `/` (home) | 190,244 ns | 529 ns | 4.38 KB, 1.71 KB | 360x |
| `/about` | 189,397 ns | 565 ns | 4.54 KB, 1.71 KB | 335x |
| `/_astro/*.css` (immutable) | 155,129 ns | 544 ns | 4.58 KB, 1.71 KB | 285x |
| `/about` with `If-None-Match` (304) | 16,856 ns | 434 ns | 2.79 KB, 1.41 KB | 39x |
| `/time` (miss, falls through to Jint) | 300 ns | 291 ns | 1.2 KB | unchanged |

Each row includes about 197 ns and 1.13 KB for creating the benchmark's `HttpContext`; net of
that a memory-served 200 costs 330-370 ns and 0.58 KB, and a 304 about 240 ns and 0.28 KB. The
original middleware measured 177-213 us (4.4-4.8 KB) for the same three files, 17.5 us for the 304,
so the `Disk` column is the same path as before.

Load test, real Kestrel, 64 connections, the same host settings before and after:

| Scenario | Before req/s | After req/s | p50 before | p50 after | p99 after |
| --- | ---: | ---: | ---: | ---: | ---: |
| `static-page` | 4,471 | 32,807 | 13.57 ms | 1.50 ms | 8.83 ms |
| `static-home` | 4,925 | 33,815 | 12.29 ms | 1.52 ms | 7.74 ms |
| `static-asset` | 5,714 | 28,830 | 10.75 ms | 1.89 ms | 7.87 ms |
| `not-modified` | 12,032 | 31,721 | 5.18 ms | 1.81 ms | 6.40 ms |

After the change the server uses 3 to 3.5 cores and the load generator 2 cores, so Kestrel and
the loopback stack are the limit, not the adapter. With 16 connections `static-page` does 34.5K
requests/s at a p50 of 162 microseconds; with 256 it does 30.4K at 7.9 ms (queueing).
Response headers of the 200, `HEAD`, 304 and 206 answers were compared with `curl -i` and are identical.

## 2. The request and response bridge

`AstroHttpBridge` turns the `HttpRequest` into the `HttpRequestMessage` that Jint takes and Jint's
`HttpResponseMessage` back into the response:

- `CreateRequestAsync` returns a `ValueTask`, shares the `HttpMethod` instances, checks hop-by-hop
  headers with a `switch` (no `HashSet`, no LINQ), has a fast path for single-value headers, and reads
  a body of up to 64 KiB into one exact-size array. A declared length over the limit is a 413
  before any byte is read.
- The response is not copied into an intermediate buffer any more. Jint already returns a
  `ByteArrayContent`, so buffering is a no-op, the engine goes back to the pool at once (before the
  response is written to a slow client), and the bytes are copied from the message to the response
  body in one call. Headers are read from the non-validated views.

`BridgeBenchmarks`, default job:

| Method | Body | Before | After | Allocated, before to after |
| --- | ---: | ---: | ---: | ---: |
| `CreateGetRequest` | - | 938-973 ns | 581-597 ns | 1,160 B to 936 B |
| `CreatePostRequest` (1 KB body) | - | 1,288-1,318 ns | 779-805 ns | 3,568 B to 2,168 B |
| `ResponseToHttpContext` | 2 KB | 1,391 ns | 816 ns | 7,432 B to 2,416 B |
| `ResponseToHttpContext` | 20 KB | 3,167 ns | 825 ns | 43,432 B to 2,416 B |
| `ResponseToHttpContext` | 200 KB | 138,832 ns | 820 ns | 403,988 B (large object heap, gen2) to 2,424 B |

`ResponseToHttpContext` includes the message (140 ns, 528 B) and the benchmark's context (197 ns,
1.13 KB), so writing the response costs about 500 ns and 0.75 KB, and no longer depends on the body size.

What is left is inside Jint, and the upstream project could remove it: `InvokeFetchHandlerAsync`
reads the request body with `ReadAsByteArrayAsync`, and its response is built with
`new ByteArrayContent(source.ToArray())`. Each is a copy of the body that the host cannot avoid.
A `byte[]` or `ReadOnlyMemory<byte>` overload would help; that is a suggestion for Jint, not something
this repository can change.

## 3. The memory limit per request

By default every request runs under `LimitMemory(128 MiB)` so a runaway page cannot take the process
down (S3, B6). It is an exact constraint that Jint checks before every statement, so what it costs
was measured with `RenderBenchmarks` (one engine, default job), `MemoryLimit = True` against `False`:

| Request | Without | With | Difference |
| --- | ---: | ---: | ---: |
| `ApiTime` | 235.8 us | 231.9 us | -2% |
| `PostEcho` | 244.7 us | 249.5 us | +2% |
| `PageTime` | 829.6 us | 842.7 us | +1.6% |
| `PageProduct` | 800.2 us | 828.4 us | +3.5% |
| `PageNotFound` | 700.4 us | 744.1 us | +6% (StdDev 38 us) |

Allocations are identical. Within noise to a few percent, so the limit stays on by default.
A hand-written constraint that is checked every 64 statements would be cheaper, but it would
lose what Jint charges for string concatenation and interop inside the exact limit.

## 4. Runtime settings

An on-demand page allocates about 300 KB per request, nearly all of it inside Jint and Astro, so the
collector is the first runtime setting to look at. The `ondemand-time` scenario (`/time`, 64
connections) was run against nine settings of the sample host. Every run is a fresh host process
(`--launch`) with 10 seconds of warm-up and 15 of measurement. There is one discarded warm-up run
first, then three repetitions, and the order of the variants is rotated between repetitions so that
drift of the machine does not favor one of them. The table shows the median of the three runs,
with the range of the throughput.

| Variant | Setting | Requests/s (range) | p50 | p99 | Server cores | Memory | GC pause |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| `base` | the sample host as shipped: Server GC, concurrent GC, pool of 28 | 6,958 (6,880-7,332) | 6.91 ms | 34.8 ms | 18.9 | 527 MB | 18.3 % |
| `workstation` | `DOTNET_gcServer=0` | 1,342 (1,340-1,454) | 40.45 ms | 113.0 ms | 5.0 | 400 MB | 81.5 % |
| `nonconc` | `DOTNET_gcConcurrent=0` | 7,113 (6,710-7,241) | 6.85 ms | 33.8 ms | 19.1 | 483 MB | 18.0 % |
| `gen0-256m` | `DOTNET_GCgen0size=10000000` (256 MB budget) | 6,236 (5,518-6,448) | 6.78 ms | 55.3 ms | 15.1 | 524 MB | 29.1 % |
| `heaps-14` | `DOTNET_GCHeapCount=E` (14 heaps) | 7,335 (6,022-7,699) | 7.17 ms | 29.7 ms | 18.9 | 755 MB | 10.3 % |
| `noaffinity` | `DOTNET_GCNoAffinitize=1` | 7,266 (6,486-7,460) | 6.91 ms | 28.4 ms | 20.6 | 524 MB | 14.3 % |
| `pool-20` | `Astro__PoolSize=20` | 7,297 (7,132-7,468) | 7.36 ms | 28.4 ms | 18.9 | 473 MB | 15.6 % |
| `pool-40` | `Astro__PoolSize=40` | 6,937 (6,462-6,945) | 7.23 ms | 34.8 ms | 19.5 | 660 MB | 14.8 % |
| `pgo-off` | `DOTNET_TieredPGO=0` | 6,797 (6,304-6,805) | 7.49 ms | 34.3 ms | 20.0 | 528 MB | 15.1 % |

The numbers of `DOTNET_` variables are hexadecimal (`E` is 14, `10000000` is 256 MB). No request
failed in any run. Server cores include the threads of the collector, and the GC pause is the share
of the wall time during which every managed thread was stopped for a collection.

What this says:

- **Server GC is required.** With workstation GC the same host does 5.2 times fewer requests, is
  paused in the collector for 81 percent of the time, and uses 5 cores instead of 19: one collector
  has to clean up after 28 engines that allocate in parallel. The sample host sets
  `ServerGarbageCollection` in its project; an application that hosts the pool should do the same
  (spike 3 has the numbers for small pools).
- **The shipped settings are the right ones.** Concurrent GC off, GC affinity off, a pool of 20 or 40
  and `TieredPGO=0` are all within 5 percent of `base`, and `base` itself differs by 6.6 percent
  between its three runs (between sessions the on-demand scenarios moved by up to 30 percent on
  this machine). A setting had to gain more than about 10 percent, outside the noise, to be adopted.
  None did, so the defaults of `AddAstro` and of the sample host did not change.
- **A bigger gen0 budget is worse.** A 256 MB budget makes a third fewer collections, but each one
  is longer: 29 percent of the time is paused instead of 18, the p99 is 55 ms instead of 35, and the
  throughput is 10 percent lower. All three runs are below the slowest `base` run.
- **Fewer heaps and no affinity shorten the pauses, not the run.** `heaps-14` (10 percent paused)
  and `noaffinity` (14 percent) bring the p99 to 28-30 ms from 35 ms, but the throughput stays inside
  the noise (the runs of `heaps-14` span 6,022 to 7,699), and `heaps-14` keeps 230 MB more in memory.
  The effect may depend on this CPU (8 fast and 12 slow cores), so it is something to try with the
  load test on the target hardware, not a default.
- **The pool needs no more than the processor count.** A pool of 20 is within noise of 28 and holds
  10 percent less memory (473 MB against 527 MB); a pool of 40 holds 25 percent more (660 MB) and
  gains nothing. The default stays at the number of processors because it needs no knowledge of the
  workload, and `PoolSize` is the lever when memory matters more than the last few percent.

This is one machine and one scenario, and combinations of settings were not tried, so read it as
"no evidence for changing the defaults", not as "no setting can help elsewhere". To repeat a variant:

```powershell
dotnet benchmarks\AstroJint.LoadTest\bin\Release\net10.0\AstroJint.LoadTest.dll `
  --launch samples\HybridHost\bin\Release\net10.0\HybridHost.dll `
  --env Astro__DistPath=<full path of samples\hybrid\dist> --env DOTNET_gcServer=0 `
  --scenario ondemand-time --gc-stats /api/host --warmup 10 --duration 15
```

## What was not done, and ideas

- **Streaming responses.** Jint's fetch-handler hosting does not take a streaming body yet, so
  responses are buffered. Time to first byte for a large page is therefore worse than on Node.
- **Precompression.** `br`/`gzip` of the prerendered files at build time and a `Content-Encoding`
  negotiation in the static fast path would cut bytes for slow links. Response compression and
  output caching of on-demand pages are better done by the reverse proxy or CDN, or by
  `UseResponseCompression()` / `UseOutputCache()` placed before `UseAstro()` in the host.
- **Startup.** ReadyToRun compilation of the host and the engine warm-up on start (`WarmUp`) were
  not measured.
- **Kestrel.** The static scenarios are limited by Kestrel and loopback at 3 to 3.5 server cores;
  options such as inline scheduling are an application-level choice and were not tried.
- **Prerender more.** A page that does not need the request should be a prerendered page (the
  default in a hybrid site). It serves 4 to 5 times as many requests per second as an on-demand
  page, 30K against 6-7K here, and needs no engine.
