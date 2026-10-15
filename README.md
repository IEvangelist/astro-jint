# astro-jint

Hybrid [Astro](https://astro.build) sites on ASP.NET Core, with no Node.js at run time.

Some pages are generated at build time, like in any static Astro site. The others are rendered **on demand**: Astro's
own server runtime runs inside [Jint](https://github.com/sebastienros/jint), a JavaScript interpreter for .NET, in an
ASP.NET Core application. One process serves both kinds of pages, and the rest of ASP.NET Core (authentication, health
checks, minimal APIs, rate limiting, logging, forwarded headers) sits in front of the site.

> **Status: prototype.** It builds and serves real Astro sites, and the end-to-end suite in this repository runs the
> tests of Astro's official adapters against it (see [Conformance](#conformance)). Nothing is published to npm or NuGet
> yet, it depends on a **preview of Jint 5**, and the [limits](#limits) are real. Tested with Astro 7.3.8, .NET 10,
> Node.js 24 and pnpm 11. Node.js and pnpm are build-time tools only.

## How it works

```text
build time: astro build (Node.js)                run time: ASP.NET Core (no Node.js)
---------------------------------                ------------------------------------------------------------
dist/client/**         prerendered pages   --->  static files middleware: ETag, ranges, cache headers
                       and hashed assets
dist/server/entry.mjs  Astro server        --->  pool of Jint engines, each one runs app.render(request)
dist/server/chunks/    runtime and routes
dist/astro-jint.json   manifest            --->  read at start-up: base, trailingSlash, static headers, routes
```

A request passes your middleware and endpoints first. Then:

1. A `GET` or `HEAD` for a file in `dist/client` (a prerendered page, an `_astro/*` asset, a file from `public/`) is
   served by the host itself, including the directory-index and trailing-slash redirects.
2. Everything else (on-demand pages, API routes, server islands, config redirects, 404s) goes to a free engine of the
   pool, which runs Astro's `app.render(request)`. The request and the response cross the bridge as plain data.

The **adapter** (`packages/astro-adapter-jint`) makes `astro build` emit that layout: a self-contained server bundle,
the client output and the manifest. The **host** (`src/AstroJint.AspNetCore`) loads the bundle into a pool of engines that
share the parsed modules and turns Astro's `Response` into an ASP.NET Core response. The experiments that shaped this
design, with measurements, are in [docs/spikes](docs/spikes).

## Quick start

### 1. The Astro project

```js
// astro.config.mjs
import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';

export default defineConfig({
  adapter: jint(),
});
```

With `output: 'static'` (the default) pages are prerendered, and a page that needs the request opts out with
`export const prerender = false;`. With `output: 'server'` it is the other way around.

### 2. The host

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAstro(options =>
{
    // Per-request data for Astro.locals, for example what ASP.NET Core authentication knows.
    options.LocalsFactory = context => new { user = context.User.Identity?.Name };
});

var app = builder.Build();
app.MapGet("/healthz", () => "ok"); // ASP.NET Core endpoints win over the site
app.UseAstro();                     // UseAstroStaticFiles() + MapAstro()
app.Run();
```

The host reads the build output from the `astro` folder next to the application (`AstroOptions.DistPath`; a relative
path is resolved against the content root). Copy the `dist` folder of the Astro project there when you publish.
`samples/HybridHost` does that with an MSBuild target.

### 3. Run the sample

```powershell
pnpm install
dotnet run --project samples/HybridHost   # builds the Astro site first when its sources changed
```

The site is at <http://localhost:5080>.

| | Pages |
| --- | --- |
| Prerendered | `/`, `/about`, `/blog`, `/blog/<slug>`, `/products`, the 404 and 500 pages; `/news` redirects to `/blog` |
| On demand | `/time`, `/session` (a visit counter), `/me` (`Astro.locals` from ASP.NET Core authentication), `/contact` (form post, cookie, redirect), `/diagnostics` (`astro:env`, client address), `/products/<id>`, `/api/time`, `/api/echo` |
| Server island | the `ServerClock` component, rendered on demand inside a prerendered page |
| Host endpoints | `/healthz`, `/api/host`, `/sign-in/<name>`, `/sign-out` |

## Configuration

### Adapter: `jint({ ... })`

| Option | Default | Purpose |
| --- | --- | --- |
| `minify` | `false` | Minify the server bundle. It becomes half the size, but parse time barely changes and stack traces get harder to read. |
| `staticHeaders` | `false` | With `security.csp`, send the policy of prerendered pages as a header instead of a `<meta>` tag, like the official adapters. The host replays the headers from the manifest. |

What the adapter sets up for you:

- `build.redirects` is turned off, so the runtime answers config redirects with their configured status instead of a
  meta-refresh page.
- The default Sharp image service is replaced by Astro's passthrough service (Sharp needs Node.js), unless you configure
  another service. The `/_image` endpoint is the adapter's own.
- Sessions work out of the box and are stored in the `IDistributedCache` of the host. Under `astro dev` they live in
  memory. `session: false` turns them off and removes the session runtime from the bundle.
- The server build is a single self-contained module graph without `node:*` imports (`ssr.noExternal`, target
  `webworker`), which is what Jint can evaluate.
- It declares static, hybrid and server output and `astro:env` secrets as supported, and Sharp and i18n domains as
  unsupported.

### Host: `AstroOptions`

Set them in `AddAstro(options => ...)`, bind them from configuration
(`builder.Services.AddOptions<AstroOptions>().BindConfiguration("Astro")`), or both, like the sample does.

| Option | Default | Purpose |
| --- | --- | --- |
| `DistPath` | `"astro"` | Folder with `astro-jint.json`, `client` and `server`. Relative to the content root. |
| `PoolSize` | processor count | Number of engines, which is the number of on-demand pages that render at the same time. Further requests wait for a free one. |
| `RequestTimeout` | 30 s | Time from the start of an on-demand request to its response, including the wait for an engine. Waiting too long answers 503 with `Retry-After: 1`, rendering too long answers 504. `Timeout.InfiniteTimeSpan` disables it. |
| `MemoryLimitBytes` | 128 MiB | What JavaScript may allocate while it renders one request (500 beyond it). `null` disables the limit. |
| `TimeZone` | UTC | Time zone that JavaScript's `Date` uses. |
| `WarmUp` | `true` | Create every engine in the background at start, so that the first on-demand request does not pay for it. Static pages are served immediately either way. |
| `MaxRequestBodyBytes` | 10 MiB | Larger request bodies get a 413. |
| `StaticFileCacheBytes` | 32 MiB | Prerendered files of up to 1 MiB are read once at start and served from memory (the smallest first, until this budget is used). `0` serves every file from disk. A file that changes after start is still sent as it was read, so restart after a deploy. |
| `LocalsFactory` | none | `Func<HttpContext, object?>`. The result is serialized to JSON (camelCase) and becomes `Astro.locals`. |
| `Env` | empty | Values for `env.schema` variables with `access: 'secret'`, also bound from `Astro:Env:NAME` or `Astro__Env__NAME`. |
| `FetchUrlFilter` | none (rejects every request) | Decides which URLs JavaScript's `fetch` may request, for example the origin of a remote image. A `data:` URL is decoded in the engine and never reaches the filter. |
| `SessionIdleTimeout` | 20 minutes | How long session data is kept after its last use. |

### Composing with ASP.NET Core

- `UseAstro()` is `UseAstroStaticFiles()` followed by `MapAstro()`. `MapAstro()` registers a fallback endpoint and
  returns an `IEndpointConventionBuilder`, so conventions such as authorization or rate limiting can apply to the
  on-demand pages only.
- Endpoints of your application win over the site: the static files middleware steps aside for a request that another
  endpoint matches.
- `console.*` output of the site goes to `ILogger` with the category `AstroJint.Console`.
- Behind a proxy, add `UseForwardedHeaders` so that Astro sees the host, protocol and address of the visitor.

## Limits

This is a prototype built on an interpreter. Know these before you plan on it:

- **Jint is not V8.** A page whose JavaScript does a lot of work costs more here than in Node.js. Small pages render in
  about a millisecond, but measure your own.
- **No Node.js APIs.** The server bundle must be self-contained: `node:*` built-ins and packages that stay external are
  not available. `astro build` warns about server code that imports something outside the bundle, and every on-demand
  route that needs that file answers 500 (all of them, for a middleware). Prerendered pages keep working. Importing other
  files of the bundle by relative path is fine.
- **`<Code />` does not render on demand.** Shiki, which the component uses, needs WebAssembly and Jint has none, so an
  on-demand page with the component answers 500. In a prerendered page it works, because the build runs in Node.js.
  `<Prism />` works in both (the adapter ships the loader of `@astrojs/prism` for workers). The cause and a workaround
  that costs 400 MiB or more of memory for the first request are in the
  [Cloudflare conformance notes](docs/conformance/cloudflare.md) (GAP-CF-11).
- **Bodies are buffered.** Requests and responses are not streamed, because Jint cannot. Large uploads and downloads
  belong in ASP.NET Core endpoints. A request body is read in full before `RequestTimeout` starts, so slow senders are
  held back by Kestrel's own limits (`MinRequestBodyDataRate`, `RequestHeadersTimeout`), which you should leave on.
- **Images are not resized.** With the passthrough service, `<Image />` and `getImage()` serve the original file. The
  image endpoint copies that file in memory several times per request, so keep the originals small. Remote images need
  `FetchUrlFilter` and `image.domains`.
- **Sessions** live in `IDistributedCache`, which is in memory unless you register another cache (Redis, SQL Server, ...),
  so they do not survive a restart or span instances by default. Astro marks the session cookie `Secure` in production,
  so visitors need HTTPS to keep their session.
- **Public `astro:env` variables** of the server are fixed at build time. Only `access: 'secret'` variables can change at
  run time.
- **`astro preview` and the dev server** do not use the host. Develop with `astro dev` and test the hosted result with
  the built site.
- **Multipart forms** whose part names are not quoted fail in `Request.formData()` (a Jint limitation). Browsers and curl
  quote them.
- **Not supported:** i18n domains and the Sharp image service. Anything else in Astro is untested until the
  [conformance matrix](docs/conformance.md) says otherwise.

## Performance

Numbers are from a 28-processor Windows workstation and the sample site. The method and every table are in
[spike 4](docs/spikes/04-host-performance.md); [benchmarks/README.md](benchmarks/README.md) shows how to repeat them.

- **Prerendered pages are served from memory.** The adapter spends about 0.35 microseconds and allocates 0.6 KB for one
  (285 to 360 times less than opening the file for each request), and the sample host serves 29,000 to 34,000 of them
  per second over 64 connections, up from 4,500 to 5,700. Kestrel and the loopback stack are the limit there, not the
  adapter.
- **On-demand pages cost what the interpreter costs.** A warm engine renders an API route in 0.25 ms and a page in 0.8
  to 0.9 ms (130 to 300 KB of garbage per request); with 28 engines the sample host answers 6,000 to 9,000 of them per
  second. The adapter's own share, translating the request and the response and handing out an engine, is a few
  microseconds, and the bridge allocates the same few KB for a 2 KB and a 200 KB body. A page that does not need the
  request should be prerendered: it serves four to five times as many requests per second and needs no engine.
- **The memory limit per request** (`MemoryLimitBytes`) costs a few percent at most.
- **Engines are cheap to add.** The first engine of a cold process needs about 0.5 s; further engines take 30 to 70 ms
  because they share the parsed modules (1.9 MB per extra engine instead of 12.5 MB), and `WarmUp` creates them in the
  background. Memory stays flat over 20,000 requests ([spike 3](docs/spikes/03-engine-pool-performance.md)).
- **Use Server GC** with more than about eight engines (the sample host does). Without it the 28-engine host did five
  times fewer on-demand requests per second and spent 80 percent of its time paused in the collector. Fewer GC heaps,
  no GC affinity, concurrent GC off, pool sizes of 20 and 40, and `TieredPGO` off did not beat the defaults beyond the
  noise; a 256 MB gen0 budget was 10 percent slower.
- Compression and caching of on-demand pages are not part of the adapter. A reverse proxy or a CDN in front of the host
  is the usual place for them.

## Repository layout

```text
packages/astro-adapter-jint        the Astro adapter (TypeScript, node:test unit tests)
src/AstroJint.AspNetCore           the ASP.NET Core library: engine pool, static files, request bridge
samples/hybrid                     sample Astro site with prerendered and on-demand pages
samples/HybridHost                 sample ASP.NET Core application that hosts it
tests/AstroJint.AspNetCore.Tests   unit and integration tests of the library
tests/AstroJint.Conformance.Tests  end-to-end suites ported from Astro's tests (fixtures: tests/conformance)
tests/AstroJint.Sample.Tests       end-to-end tests of the sample site and host
benchmarks/AstroJint.Benchmarks    BenchmarkDotNet suites: request bridge, static files, path resolution, rendering
benchmarks/AstroJint.LoadTest      load generator that starts the sample host and reports throughput, latency and GC
docs/conformance.md                which upstream tests are ported, and how they differ
docs/spikes                        findings of the experiments behind the design
spikes                             the throwaway code of those experiments
```

## Development

You need the .NET 10 SDK, Node.js 22.12 or later and pnpm 11.

```powershell
pnpm install
pnpm build                                   # the adapter and the sample site
dotnet build AstroJint.slnx

pnpm --filter astro-adapter-jint test        # adapter unit tests
dotnet test --project tests/AstroJint.AspNetCore.Tests/AstroJint.AspNetCore.Tests.csproj
dotnet test --project tests/AstroJint.Sample.Tests/AstroJint.Sample.Tests.csproj
dotnet test --project tests/AstroJint.Conformance.Tests/AstroJint.Conformance.Tests.csproj
```

- The conformance suite builds the adapter and every Astro fixture it needs on demand, so `pnpm install` is all it needs
  beforehand. The first run takes minutes; later runs reuse the builds until sources or the adapter change.
  `CONFORMANCE_BUILD_SLOTS` caps the parallel `astro build`s (default: a quarter of the cores, at least two), which helps on
  machines that are short of memory.
- Run a part of it with a filter, for example
  `dotnet test --project tests/AstroJint.Conformance.Tests/AstroJint.Conformance.Tests.csproj -- --filter-namespace AstroJint.Conformance.Node`.
- If your machine's npm configuration points at a private registry, set `pnpm_config_registry=https://registry.npmjs.org/`
  before `pnpm install`, so that `pnpm-lock.yaml` never records its URLs.
- The build runs the .NET analyzers (`AnalysisMode=Recommended`, code style enforced) and the library also turns on every
  performance rule and treats diagnostics as errors, because it sits on the path of each request. Every project builds with
  zero warnings; keep it that way.
- Benchmarks and the load test are not tests and do not run in CI. See [benchmarks/README.md](benchmarks/README.md), for
  example `dotnet run -c Release --project benchmarks/AstroJint.Benchmarks -- --filter "*Static*"`.

## Conformance

The tests of Astro's official adapters (Node.js, Netlify, Vercel and Cloudflare) and the adapter-facing tests of Astro
core are ported to end-to-end tests: each one builds a real Astro fixture, starts a real Kestrel host and talks to it over
HTTP. Tests that exercise a platform service (Netlify's image CDN, Vercel's ISR, Cloudflare bindings and the like) cannot
apply to this host. The matrix in [docs/conformance.md](docs/conformance.md) lists every upstream test file with what is
ported, adapted, a known gap or not applicable, and why.

## Ideas

- Answer config redirects in the host from a table in the manifest, before the app, which saves an engine round trip and
  avoids the status quirks of Astro's runtime redirects.
- Shims for Node.js built-ins (the way Nitro uses unenv), so that more npm packages run in the engine.
- Image optimization on the .NET side.
- Streaming responses.
- Precompressed prerendered files (`br` and `gzip` made at build time, chosen by `Accept-Encoding` in the static fast
  path), and a `byte[]` or `ReadOnlyMemory<byte>` overload of Jint's fetch-handler hosting, which would save a copy of
  every request and response body.
- Publish the packages, and choose a license.
