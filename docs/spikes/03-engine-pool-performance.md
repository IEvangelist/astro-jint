# Spike S3: engine pool, performance and limits

**Questions.**

1. What does a Jint engine cost to create, and what does a request cost once warm?
2. How many engines should the pool hold, and is one parsed module safe to share between
   concurrently running engines?
3. Does memory stay flat over many requests, or do engines need recycling?
4. Can a runaway page (infinite loop, runaway recursion, allocation bomb) be stopped without
   losing the engine or the process?

**Setup.** `spikes/JintPerf` drives the S1/S2 bundle through `InvokeFetchHandlerAsync` with
no HTTP in between, so it measures the engine alone (Kestrel and the request/response bridge
are measured with the real host later). Jint `5.0.0-preview-2249`, .NET 10.0.13 (Release,
default TieredPGO), Windows 11, Intel i7-14700K (28 logical processors), 32 GB. Bundles:
`e3` (631 KB, unminified) and `e3m` (314 KB, minified, see B5).

```powershell
$env:SPIKE = 'e3';  pnpm --filter spike-astro-app exec astro build
$env:SPIKE = 'e3m'; pnpm --filter spike-astro-app exec astro build
dotnet build spikes/JintPerf -c Release
dotnet run --project spikes/JintPerf -c Release --no-build -- spikes/astro-app/dist-e3/server [sanity|startup|latency|throughput|memory|limits]
$env:DOTNET_gcServer = '1'      # repeat `throughput` with Server GC
```

Workstation (concurrent) GC is the default for a console process and was used unless stated.
Every page response in the throughput run is checked for the exact id and query string it
asked for, so any cross-request contamination would show up as a "bad" response.

## B1 Startup

| What | Time |
| --- | --- |
| `Engine.PrepareModule` (parse only, 617 KB source) | 90-130 ms |
| First engine in a cold process (new + prelude + import + first request) | ~430-480 ms (Jint JIT warm-up) |
| Further engine, **parsing the bundle itself** | 110-130 ms to ready |
| Further engine, **sharing one `Prepared<Module>`** | 30-70 ms to ready (evaluate 20-75 ms, first request 3-5 ms, occasionally 25-35 ms) |
| Second request on a fresh engine | 1.1-1.7 ms |
| Pre-warm 16 engines sharing the module, sequential | 710 ms |
| Pre-warm 16 engines sharing the module, parallel | **287 ms** |
| Pre-warm 24 engines in parallel | 740-780 ms |

Parsing dominates the per-engine cost and is paid once when the module is shared.
`new Engine(...)` itself is ~0.1 ms after the first one.

## B2 Warm latency (one engine, sequential, 2000 requests after 300 warm-up)

| Scenario | p50 | p95 | p99 | max |
| --- | --- | --- | --- | --- |
| `GET /time` | 0.86 ms | 2.28 | 3.63 | 10.2 |
| `GET /items/{n}?q=x` | 0.58 ms | 1.13 | 1.95 | 7.2 |
| `POST /api/echo` (endpoint, JSON) | 0.28 ms | 0.54 | 1.67 | 4.7 |
| `GET /nope` (404 page) | 0.27 ms | 0.48 | 1.33 | 6.6 |
| `GET /crypto` (`randomUUID` + SHA-256) | 0.41 ms | 0.67 | 1.64 | 3.4 |
| `GET /delay` (page awaits a 25 ms `setTimeout`) | 33.0 ms | 38.0 | 39.7 | 47.4 |

A warm engine renders a page in well under a millisecond, so one engine serves roughly
1,200-1,900 requests per second. Timers are honored but cost 8 ms or so of slop beyond the
requested delay.

## B3 Throughput versus pool size

Closed loop over a bounded channel of engines, `GET /items/{n}?q=x{n}`, 4 s per cell.
**0 bad responses in all 24 cells (about 490,000 requests)**, including 96 clients on 24
engines, so sharing one `Prepared<Module>` between concurrently running engines is safe.

| Pool | Workstation GC, clients = pool | Workstation GC, clients = 4x | Server GC, clients = pool | Server GC, clients = 4x |
| ---: | ---: | ---: | ---: | ---: |
| 1 | 1,205 req/s | 1,924 | 811 | 1,617 |
| 2 | 3,707 | 3,379 | 2,472 | 2,502 |
| 4 | 5,671 | 5,233 | 4,618 | 4,515 |
| 8 | 5,570 | 4,836 | 5,360 | 6,416 |
| 16 | 3,298 | 2,610 | 11,404 | 11,721 |
| 24 | 2,917 | 3,196 | **14,017** | 12,930 |

Each request allocates about 150 KB. With workstation GC throughput peaks at 4-8 engines and
then **falls** (service p95 jumps from ~1 ms to 23-33 ms at 16-24 engines, because every
gen0 collection suspends all the allocating threads). Server GC keeps scaling to roughly the
number of cores (service p95 under 6 ms at 24 engines). With Server GC, waiting for a free
engine adds 2.5-4 ms at p50 when there are 4x as many clients as engines.

## B4 Memory

| What | Result |
| --- | --- |
| Parsed AST of the bundle (`Prepared<Module>`, shared) | 10.2 MB |
| 8 engines, each parsing the bundle | 12.5 MB per engine (99.7 MB total) |
| 8 engines sharing one `Prepared<Module>` | **1.9 MB per engine** (15.5 MB total) |
| 20,000 mixed requests on one engine, live heap after a forced GC | 32.6, 33.1, 33.2, 29.7, 29.7, 29.7 MB (every 4,000 requests): flat |
| Allocation per request | ~151 KB (.NET side), ~176 KB (Jint's own estimate) |
| Latency drift over the run | 1.11 ms per request in the first window, 0.54 ms in the last (JIT tiering) |

There is no leak signal, so periodic engine recycling is not needed for memory.

## B5 Minified bundle (`e3m`)

Astro hard-codes `build.minify: false` for the server build, so minification can only be
requested through `environments.ssr.build.rolldownOptions.output.minify` (see S1/S2 notes
and `spikes/astro-app/astro.config.mjs`). The result is half the size (631 KB to 314 KB) but
the parse time barely moves (89-131 ms versus 94-127 ms):

| | e3 | e3m |
| --- | --- | --- |
| Engine from shared `Prepared<Module>`, ready | 30-106 ms | 21-56 ms |
| Pre-warm 16 engines, sequential / parallel | 710 / 287 ms | 457 / 232 ms |
| Warm latency | see B2 | same within noise |

Stack traces from a minified bundle are much harder to read. **Decision:** minification is an
opt-in adapter option, off by default.

## B6 Timeouts, cancellation and limits

| # | Scenario | Result |
| --- | --- | --- |
| 6a | Token only (no constraint), page busy-loops 800 ms, token cancels at 200 ms | `OperationCanceledException` after 207 ms. The next request on the same engine succeeds |
| 6g | Token only, page runs `while (true) {}`, token cancels at 200 ms | `OperationCanceledException` after ~205 ms. No thread keeps spinning afterwards (the one busy thread in the next second is tiered JIT work, back to ~16 ms in the second after). Next request 2-8 ms |
| 6b | `OperationDeadlineConstraint` 200 ms, `while (true) {}` | `TimeoutException` after 200 ms (454 ms once, on a brand-new engine). Engine reusable, still correct after a 500 ms idle gap |
| 6c | Deadline 30 s plus token cancelled at 150 ms | `OperationCanceledException` after 154 ms |
| 6d | `/delay` (25 ms timer) with a 5 ms budget, or a token cancelled at 5 ms | `TimeoutException` after 19-36 ms, or `OperationCanceledException` after 16-18 ms. Engine reusable. A 2 s budget lets the page finish normally |
| 6e | Unbounded recursion in a page, default options | The page fails and Astro answers 500 in ~41 ms. The process and the engine survive (the default recursion guard works) |
| 6f | `LimitMemory(32 MB)` and an allocation bomb | `MemoryLimitExceededException` after 58-79 ms. Engine reusable. 300 normal requests afterwards: 0 failures; a normal request allocates ~176 KB JS-side (p99 181 KB) |
| 6h | `LimitMemory(256 MB)` on normal requests | No measurable overhead (mean 0.43-0.48 ms versus 0.45-0.61 ms) |

The important correction to the earlier research notes: for `InvokeFetchHandlerAsync` the
`CancellationToken` is **not** only wired to `request.signal`. The method reserves the engine
with that token as its entry token, so the token stops the handler's own run, even a
synchronous infinite loop, and any wait inside it. (The sentence "it is not an execution
constraint" in Jint's docs describes the polled `InvokeFetchHandler(request, requestAborted)`
overload.) A linked `CancellationTokenSource` with `CancelAfter(timeout)` is therefore a
sufficient whole-request time limit, and `OperationDeadlineConstraint` is not needed.

## Decisions for the host

1. **Pool of engines, one parsed module shared by all of them.** Parse the bundle once
   (`Engine.PrepareModule`), import it into each engine with `ModuleBuilder.AddModule(prepared)`.
   It cuts per-engine memory by 85% and per-engine startup by 60-70%, and was contamination-free
   under concurrency.
2. **Default pool size: `Environment.ProcessorCount`, with Server GC.** The sample host sets
   `<ServerGarbageCollection>true</ServerGarbageCollection>`; the host library logs a warning
   when the pool is larger than 8 and Server GC is off, because that combination performs worse
   than a small pool.
3. **Eager, parallel, background pre-warm.** The pool starts creating engines when the
   application starts; static files are served immediately and a dynamic request waits for the
   first free engine (the first engine in a cold process takes ~0.5 s).
4. **Per-request time budget through the token.** Link `HttpContext.RequestAborted` with a
   timeout (`CancelAfter(RequestTimeout)`) and pass the linked token to
   `InvokeFetchHandlerAsync`. Timeout maps to 504; a client abort just stops the work.
5. **Retire the engine after a timeout, a cancellation or a limit exception** and build a
   replacement in the background. The engine stays usable in every scenario above, but an
   abandoned request can leave jobs (timers, an abort job) on its event loop, and a
   replacement costs only 30-70 ms off the request path. A page that merely returns 500 or
   throws a normal JavaScript error keeps its engine.
6. **Allocation budget on by default**, `LimitMemory(128 MB)` per request, configurable and
   disableable. It has no measurable cost and about 700x headroom over a normal request.
7. **No extra recursion limit**: Jint's default guard already turns runaway recursion into a
   failed request.
8. **No periodic recycling** (flat heap over 20,000 requests).
9. **Minification stays opt-in** (B5).

## Consequences and caveats

- **Module-scope state lives once per engine.** A page or integration that keeps a cache, a
  counter, an in-memory session store or a rate limiter in a module variable gets N independent
  copies (one per pooled engine). Shared state must live in the host (database, distributed
  cache, a host function), and `astro:session` will need a host-backed driver.
- One engine handles one request at a time; `async` handlers interleave only inside their own
  request. Concurrency comes from the pool.
- Numbers come from one machine with a hybrid P/E-core CPU and one small page type. Larger
  pages (a Starlight docs site) cost more per request and per engine; the sample host reports
  its own numbers when it is exercised.
- The first timeout on a cold engine overshot its budget once (454 ms instead of 200 ms) and
  never again, which looks like a one-time cost of the exception path.
