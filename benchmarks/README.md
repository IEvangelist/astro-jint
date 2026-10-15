# Benchmarks

Two tools measure the C# side of the adapter. Neither is part of the test suite, and neither runs
in CI: numbers depend on the machine, so they are for looking at a change, not for gating one.

| Tool | Measures | Use it to answer |
| --- | --- | --- |
| `AstroJint.Benchmarks` (BenchmarkDotNet) | One operation in this process, no sockets and no Kestrel | What does this code path cost in nanoseconds and bytes? |
| `AstroJint.LoadTest` | A real host, over loopback, with many keep-alive connections | How many requests per second does the whole pipeline serve, and what do the CPU and the GC do? |

Both use the sample site (`samples/hybrid`), so build it first:

```powershell
pnpm install
pnpm --filter "hybrid..." build          # writes samples/hybrid/dist
```

`ASTRO_JINT_DIST` (BenchmarkDotNet) and `Astro__DistPath` (the sample host) point at another build output.

## BenchmarkDotNet

```powershell
dotnet build benchmarks/AstroJint.Benchmarks -c Release
dotnet run -c Release --no-build --project benchmarks/AstroJint.Benchmarks -- --filter "*Static*"
dotnet run -c Release --no-build --project benchmarks/AstroJint.Benchmarks -- --filter "*" --quick --artifacts D:\temp\bdn
```

`--quick` runs a few short iterations: enough to see a change, not enough to publish a number. Every
benchmark runs in this process (`InProcessNoEmitToolchain`) with the memory diagnoser, so the
`Allocated` column is the bytes that one operation allocates.

| Class | What it runs |
| --- | --- |
| `StaticFilesBenchmarks` | A request for a prerendered file through the static files middleware: home, page, hashed asset, `304 Not Modified`, and a miss that falls through to Jint. `Cache` is `True` (files held in memory, the default) or `False` (read from disk for every request). `ContextOnly` is the cost of making the request, to subtract |
| `ResolverBenchmarks` | Mapping a request path to a prerendered file, for paths that match and paths that do not. Every request does this first |
| `BridgeBenchmarks` | `HttpRequest` to `HttpRequestMessage`, and Jint's response message to the `HttpResponse`, for bodies of 2, 20 and 200 KB |
| `RenderBenchmarks` | A whole on-demand request through one engine of the pool: an API route, pages, a 404, a POST. `MemoryLimit` is `True` (the default 128 MiB allocation budget per request) or `False` |

The requests are made on a `DefaultHttpContext`, so Kestrel's parsing of the request and its writing
of the response are not in these numbers. Each benchmark checks the status of what it measures in
its setup: a benchmark of a request that failed would measure the wrong thing.

## Load test

```powershell
dotnet build samples/HybridHost -c Release -p:SkipAstroSiteBuild=true
dotnet build benchmarks/AstroJint.LoadTest -c Release
dotnet benchmarks\AstroJint.LoadTest\bin\Release\net10.0\AstroJint.LoadTest.dll `
  --launch samples\HybridHost\bin\Release\net10.0\HybridHost.dll `
  --env Astro__DistPath=D:\path\to\samples\hybrid\dist `
  --scenario all --gc-stats /api/host --warmup 10 --duration 15
```

The tool opens 64 keep-alive connections (`--connections`), sends the requests of the scenario over
raw sockets, with the headers of a browser, and checks every status. The report is a Markdown table.

| Option | Meaning |
| --- | --- |
| `--scenario` | `static-page`, `static-home`, `static-asset`, `not-modified`, `ondemand-time`, `ondemand-product`, `ondemand-post`, `mixed` (7 static requests to 3 on-demand ones), or `all` |
| `--launch <host.dll>` | Start `dotnet <host.dll>` hidden, on `--url`, wait for `/healthz`, measure that process and stop it at the end. A fresh process per run keeps the runs independent |
| `--env NAME=VALUE` | With `--launch`, set a variable for the host (repeatable): `--env DOTNET_gcServer=0`, `--env Astro__PoolSize=20` |
| `--pid <id>` | Instead of `--launch`: measure a host that is already running. The report adds `Server cores`, `Generator cores` and `Server memory` |
| `--gc-stats <path>` | Add `GC pause`, `GCs (gen0/1/2)` and `Allocated/request`, read from the JSON of that path (the sample host serves it at `/api/host`) before and after the measurement |
| `--connections`, `--duration`, `--warmup` | Concurrency and seconds of measurement and of warm-up (default 64, 10, 3) |

Reading the numbers:

- `Server cores` and `Generator cores` are the processor time that each process used while it was
  measured, divided by the wall time. If the generator needs most of a core, it is the limit.
- The load generator shares the machine with the server. On a hybrid P/E-core CPU, with other programs
  running, the same on-demand scenario varied by up to about 30 percent from run to run. Repeat a run,
  alternate the variants you compare, and compare medians. A difference below 10 percent in one run is noise.
- Static scenarios are fast enough that the connection count matters: 16 connections give the lowest
  latency, 64 the default, 256 start to queue.
- `GC pause` is the share of wall time that the process spent paused by the garbage collector.
  `GCs (gen0/1/2)` are `GC.CollectionCount(0)`, `(1)` and `(2)` over the measurement, so the first
  number is all collections, the second those that also collected generation 1, the third those that
  also collected generation 2.

The settings of the host (Server GC, pool size) are the ones that are measured, so pass them with
`--env` rather than editing the host.
