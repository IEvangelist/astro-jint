# Conformance with the official adapters

Astro ships adapters for Node.js, Cloudflare, Netlify and Vercel, with a large test suite each, and `astro` itself has tests for what a deployment has to get right: routing, middleware, sessions, server islands, redirects, the image endpoint, response headers. This project ports the tests that make sense for a host that runs the server build in Jint, so that "a site behaves the same behind this adapter" is checked, not assumed. This page says how the tests are ported, what the numbers are, where the behavior differs, and what is still open.

## How the tests are ported

Upstream is `withastro/astro` at commit `71e2b24b` (Astro 7.3.8; `@astrojs/node` 11.1.7, `@astrojs/cloudflare` 14.3.4, `@astrojs/netlify` 8.2.9, `@astrojs/vercel` 11.0.14).

- **One class per upstream file**, in `tests/AstroJint.Conformance.Tests/<Area>/`. `[Trait("Upstream", "node/url.test.ts")]` names the file that a class ports, and the display name of a test is the title of the upstream `it`, so a test can be looked up in either repository by name.
- **The fixtures are real Astro projects**, in `tests/conformance/fixtures/<name>`: the upstream fixtures (several folded into one, to keep the number of builds down) with `adapter: jint()` in place of the official adapter. The harness runs `astro build` for a fixture when a test first needs it, and again when the fixture, `tests/conformance/support` or the adapter changed, so the tests always run against a real build output.
- **The tests talk HTTP to the real host.** `SiteHost` starts a build in an ASP.NET Core host (`AddAstro` and `UseAstro` on Kestrel, a loopback socket) once per run and shares it between the classes, like the `before()` hooks upstream share one server. Nothing is mocked: a request takes the route it takes in production.
- **Variants.** A test that upstream hands other config (`loadFixture({ output: 'server', base: '/docs' })`) asks for a variant: the config is merged over the fixture's (`tests/conformance/support/variant.mjs`) and built into a folder of its own, `dist-<hash>`.
- **Artifacts become behavior.** The official adapters write platform files (`_redirects`, `_headers`, `.vercel/output/config.json`, `.netlify/v1/config.json`, Wrangler configuration) and many upstream tests read them. This adapter writes `astro-jint.json` and serves everything itself, so those tests assert what the file is for, over HTTP: the status of a redirect, the `Cache-Control` of a file, the CSP of a prerendered page.

Every upstream `it` (counted with `it(`, `it.skip(` and `test(`) is in one of four classes. The matrix of an area lists its files with the counts.

| Class | Meaning |
| --- | --- |
| ported | The same assertions as upstream, with the request sent to the host instead of to a handler or a function |
| adapted | The same intent, changed to fit this host: another artifact is checked, a size is scaled, fixtures are merged. The matrix says what changed and why |
| gap | The behavior is missing or different in this adapter. The test exists and is skipped, with a `GAP-` id and a way to reproduce it (see [Gaps](#gaps)) |
| N/A | The test is about something this host does not have: the dev server, the packaging of a platform's functions, a platform's own configuration files |

## The numbers

Upstream has 290 test files with 2463 `it`s in scope. Each `it` is classified in the matrix of its area:

| Area | Files | `it`s | ported | adapted | gap | N/A |
| --- | ---: | ---: | ---: | ---: | ---: | ---: |
| [Node.js](conformance/node.md) | 28 | 195 | 123 | 56 | 0 | 16 |
| [Netlify](conformance/netlify.md) | 15 | 63 | 6 | 16 | 0 | 41 |
| [Vercel](conformance/vercel.md) | 24 | 99 | 0 | 55 | 2 | 42 |
| [Cloudflare](conformance/cloudflare.md) | 64 | 311 | 32 | 63 | 15 | 201 |
| [Core, adapter-facing](conformance/core.md) | 50 | 401 | 167 | 87 | 6 | 141 |
| [Core, build and assets](conformance/core-build.md) | 20 | 138 | 107 | 13 | 0 | 18 |
| [Core, unit tests](conformance/core-units.md) | 89 | 1256 | 92 | 1070 | 23 | 71 |
| **Total** | **290** | **2463** | **527** | **1360** | **46** | **530** |

Of the unit tests of `astro`, the 89 files whose behavior a request to the host can reach are in scope. The other 205 files (2382 `it`s: build and dev-server plumbing, image and font assets, the manifest and build-time services, pure helpers of the bundle) are grouped under [Not applicable](conformance/core-units.md#not-applicable) and are not in the totals; with them, `test/units` holds 294 files and 3638 `it`s. The tests of the sample site ([sample.md](conformance/sample.md)) have no upstream counterpart: they are in their own project, `tests/AstroJint.Sample.Tests`, and are not counted here either.

What the C# suite (`tests/AstroJint.Conformance.Tests`) made of it in its last full run:

| Area | Tests | Passed | Skipped | Failed |
| --- | ---: | ---: | ---: | ---: |
| Node.js | 188 | 188 | 0 | 0 |
| Netlify | 32 | 32 | 0 | 0 |
| Vercel | 105 | 103 | 2 | 0 |
| Cloudflare | 214 | 199 | 15 | 0 |
| Core, adapter-facing | 275 | 268 | 7 | 0 |
| Core, build and assets | 137 | 137 | 0 | 0 |
| Core, unit tests | 1242 | 1218 | 24 | 0 |
| Host | 51 | 51 | 0 | 0 |
| **Total** | **2244** | **2196** | **48** | **0** |

The tables count different things: the first one counts the `it`s of upstream, the second one the tests of C#. An `it` can become several tests (the cases of a theory), a port can add tests of its own, and an N/A `it` has no test. `Host` is made of such added tests only: 51 tests of contracts that belong to this host and have no upstream counterpart, namely request and response bodies, `console`, content collections on demand, what the host hands to the bundle (client address, locals, `astro:env`), the image endpoint, sessions in `IDistributedCache` and static headers.

The 46 gap `it`s are 48 skipped tests, because two tests that a port adds are skipped for a gap as well: one more test of `request-signal` ([GAP-CORE-2](#gap-core-2)) and the multipart upload through `HttpClient` in the port of `SsrApiRouteTests` ([GAP-SAMPLE-1](#gap-sample-1)).

On the development machine (an i7-14700K with 32 GB of memory, Windows 11, .NET 10), a run that had to build everything, all 302 outputs of the fixtures and their variants, one at a time, took about 7 minutes. A run that finds all the outputs built already took 24 seconds.

## Where behavior differs

What follows is the short list; each matrix has the full one, with the test class that shows it.

### The host

- **No platform runtime.** There is no Node.js, no Workers runtime, no function packaging and no file tracing. The host serves `client/` and runs `server/entry.mjs` in a pool of Jint engines. Tests of function packaging (`includeFiles`, `@vercel/nft`, `maxDuration`, streaming flags), of edge functions, of platform configuration files, of the dev server and of `astro preview` are not applicable.
- **Bodies are buffered.** Jint cannot stream a body across the .NET boundary, so the host reads the whole request body (at most `MaxRequestBodyBytes`, 10 MiB, then 413) and the whole response before it answers. The tests about big or chunked bodies are scaled (8 MiB for 256 MiB) or not applicable. A client that goes away cancels the render and the engine is replaced, because a script cannot observe the `cancel()` of a stream.
- **Run-time limits.** Upstream has no counterpart for a render timeout (504), an exhausted pool (503 with `Retry-After`) or the memory limit of an engine (500); see [Limits](../README.md#limits).
- **Status text.** `Response.statusText` reaches an HTTP/1.1 client as the reason phrase. Text that is not printable ASCII (CR, LF, NUL, anything above 0x7E) is ignored, so a script cannot split the status line; HTTP/2 has no reason phrase.
- **A client that leaves.** The render is cancelled and the engine is replaced, but `request.signal` of the script never aborts ([GAP-CORE-2](#gap-core-2)).

### Requests and static files

- **`Host` and `X-Forwarded-Proto`.** With `security.allowedDomains` set, the URL of a request takes the `Host` and the protocol of `X-Forwarded-Proto` only when the list names them; any other `Host` becomes `localhost` and the port of the server, as behind the Node adapter. Without `allowedDomains` the host keeps the `Host` that ASP.NET Core resolved, where the Node adapter answers with `localhost` ([GAP-UNITS-7](#gap-units-7)); the `AllowedHosts` setting guards that case.
- **`base` and static files.** The host serves static files only below `base`. The Node adapter also serves them outside of it, and the upstream fixtures set `base: '/some-base'` that `loadFixture()` merges the inline config over, so upstream's "without base" variants of `trailing-slash.test.ts` do have a base. Here those variants have no base (`Node/TrailingSlashTests.cs`).
- **Static file headers.** Files below the assets folder (`_astro/`, or `build.assets`) carry `Cache-Control: public, max-age=31536000, immutable`, only on 200, 206 and 304 answers; other files carry an ETag and `public, max-age=0, must-revalidate`. A malformed `If-Match` fails with 412, where ASP.NET Core would ignore it.
- **Backslashes.** A backslash that a client writes in a path is folded into a slash before Astro sees it (`/\example.com/press` is `//example.com/press`), as behind the Node adapter.
- **Static headers.** Upstream's `_headers` files are replaced by the headers that Astro derives from `security.csp` for prerendered pages, written into `astro-jint.json` keyed by file and sent by the host with the file (`staticHeaders: true`), also when Astro answers with a prerendered 404 or 500 page. Other headers of the Netlify and Vercel configuration have no counterpart.

### Server code

- **Relative imports only.** The host loads the server bundle with its own module loader, which accepts imports of files in the bundle and nothing else, so there are no Node built-ins and no bare specifiers. A build that leaves such an import in the bundle prints a warning ([GAP-NETLIFY-1](#gap-netlify-1)).
- **A code-split server build.** The adapter builds the server as `entry.mjs` plus `chunks/*.mjs`; the pages, the middleware and the server islands are chunks that an `import()` loads when a request first needs them. A single file is not an option: Astro adds the manifest and the assets of content collection entries to the bundle in two passes, and the second overwrites the first when everything is in one chunk, so every request to a page that renders an MDX entry would answer 500 (`content-collection` fixture, `Host/ContentCollectionHostTests.cs`).
- **Locals.** `AstroOptions.LocalsFactory` takes the place of the `locals` argument of the Node handler, and the `x-astro-locals` header of the Vercel adapter means nothing here.
- **Environment.** Public `astro:env` variables are inlined when the site is built; only secrets can be supplied by the host (`Astro:Env:NAME` in configuration or `AstroOptions.Env`).
- **Images.** `sharp` is native and Jint cannot load it, so the image service passes pictures through: no resizing, no format conversion, at build time or at run time ([GAP-CF-5](#gap-cf-5), [GAP-CF-6](#gap-cf-6), [GAP-CF-7](#gap-cf-7)). A remote picture needs `AstroOptions.FetchUrlFilter` to allow its origin and `image.domains` or `remotePatterns` to list it. The engine has no outbound network of its own, with one exception: the `fetch` of Jint speaks HTTP and HTTPS only (it does not take a `data:` URL yet; asked for in [jint#4270](https://github.com/sebastienros/jint/issues/4270)), so the adapter answers `data:` URLs inside the engine (`withDataUrls`), as the Fetch standard has it, and such a URL never reaches `FetchUrlFilter`.
- **`Code` on on-demand pages.** The component needs `WebAssembly` for Shiki's Oniguruma engine and Jint has none, so a page that renders it at run time answers 500 ([GAP-CF-11](#gap-cf-11)). On a prerendered page the build, which runs in Node.js, highlights it.
- **Sessions** live in the application's `IDistributedCache` instead of the file system (Node), Netlify Blobs or the other drivers. A session written by one engine of the pool is read by another. Astro marks its cookie `Secure` in production builds, so a client needs https to send it back.

### Redirects

- **Answered at run time.** The adapter builds with `build.redirects: false`, so redirects have no files and Astro answers them in the app, after its own trailing-slash handling. With `trailingSlash: 'always'`, `/one` goes to `/one/` first and only then to its destination, where the Vercel route table answers in one step. The two [gaps](#gaps) in Vercel's redirect tests are Astro's own run-time behavior, which the Node adapter shows as well.

## Gaps

A gap is a behavior that differs from the official adapters, where the test exists and is skipped. The ones below are open; a gap that was fixed is a passing test, not an entry.

| Id | What | Status |
| --- | --- | --- |
| <a id="gap-core-2"></a>`GAP-CORE-2` | `request.signal` never aborts in the script when the client disconnects: the host stops waiting and replaces the engine, so no `abort` listener runs and a streaming endpoint never learns that the client left | Suspected: `AstroApp.RenderAsync` and the `Request` that the engine makes. 3 skipped tests. [core.md](conformance/core.md) |
| <a id="gap-core-4"></a>`GAP-CORE-4` | An integration that calls `setPrerenderer` cannot build with this adapter: the build stops with "rolldownOptions.input should not be an html file when building for SSR" | Astro leaves the `prerender` environment without an input for an adapter that is not "legacy", and counts `entrypointResolution: 'auto'` as one (its comment says `'self'`). 4 skipped tests; a Vite plugin that sets the input is the workaround. [core.md](conformance/core.md) |
| <a id="gap-cf-1"></a>`GAP-CF-1` | A page that imports a `.sql` file fails the build: Vite parses it as JavaScript, where Workers has a native `.sql` module type | The adapter has no plain-text handling for `.sql`. [cloudflare.md](conformance/cloudflare.md) |
| <a id="gap-cf-3"></a>`GAP-CF-3` | Cookies set before a page throws are dropped from the custom 500 page | The adapter renders with `app.render()`; Cloudflare keeps them through `astro/fetch` (`FetchState`, `finalize()`). [cloudflare.md](conformance/cloudflare.md) |
| <a id="gap-cf-4"></a>`GAP-CF-4` | The `onRequest` of a cache provider gets no `waitUntil` | The host has no hook for work after the response. [cloudflare.md](conformance/cloudflare.md) |
| <a id="gap-cf-5"></a>`GAP-CF-5` | The build copies pictures instead of converting them: a request for WebP or AVIF comes out as a `.jpg` copy | The adapter swaps Sharp for the pass-through service in the build too. It should keep Sharp there and swap only the service of the server bundle. [cloudflare.md](conformance/cloudflare.md) |
| <a id="gap-cf-6"></a>`GAP-CF-6` | `/_image` never converts or resizes at run time: every request answers the bytes and the type of the source | There is no encoder that runs in Jint (Sharp is native) and no hook to convert on the .NET side; see [Ideas](../README.md#ideas). [cloudflare.md](conformance/cloudflare.md) |
| <a id="gap-cf-7"></a>`GAP-CF-7` | An image service of the site that is backed by Sharp fails the build with `MissingSharp` | `ssr.noExternal: true` bundles Sharp into the prerender chunk. It should stay external in the Node prerender environment. [cloudflare.md](conformance/cloudflare.md) |
| <a id="gap-cf-11"></a>`GAP-CF-11` | The `Code` component fails on an on-demand page with `WebAssembly is not defined` (the Oniguruma engine of Shiki). On a prerendered page the build highlights it | A fix is verified (the JavaScript regex engine of Shiki, resolved for `#shiki-engine`) but needs at least 512 MiB of `MemoryLimitBytes`; whether to ship it is undecided. [cloudflare.md](conformance/cloudflare.md) |
| <a id="gap-netlify-1"></a>`GAP-NETLIFY-1` | Server code that imports a Node built-in (`node:https`) builds, but the file that has the import cannot be loaded, so every on-demand request that needs it answers 500 (all of them, for a middleware); prerendered files are still served | A limitation: the build prints a warning and a test pins the behavior. Polyfills in the style of `unenv` are future work. [netlify.md](conformance/netlify.md#gap-netlify-1-node-prefixed-imports-of-node-built-ins-in-server-code) |
| <a id="gap-vercel-1"></a>`GAP-VERCEL-1` | A redirect to another site with a configured status of 302 answers 301 | Astro core: `computeRedirectStatus` uses the configured status only when a redirect route exists. A redirect table in the host would fix it. [vercel.md](conformance/vercel.md) |
| <a id="gap-vercel-2"></a>`GAP-VERCEL-2` | With `trailingSlash: 'always'`, a redirect whose source looks like a file (`/Basic/http-2-0.html`) is a 404 without the trailing slash | Astro core, as above. [vercel.md](conformance/vercel.md) |
| <a id="gap-sample-1"></a>`GAP-SAMPLE-1` | `Request.formData()` rejects a multipart body that .NET's `MultipartFormDataContent` writes: an unquoted `name` or `filename`, and the extra `filename*` parameter, each break it. Browsers, `curl` and url-encoded forms are not affected | Jint 5.0.0-preview-2249 and 2253. Reported as [jint#4269](https://github.com/sebastienros/jint/issues/4269). One skipped test of [core-units.md](conformance/core-units.md#gaps) shows it too (GAP-UNITS-3). [sample.md](conformance/sample.md) |
| <a id="gap-units-1"></a>`GAP-UNITS-1` | A path that is percent-encoded eleven times reaches Astro as ten and is still decoded, so it answers 401, 404 or with a redirect where the Node adapter answers 400 (Astro decodes at most ten levels). A path encoded twelve times is rejected, as upstream | Kestrel decodes one level into `Request.Path`. Suspected: `AstroHttpBridge.GetPath`, which could take the path from `IHttpRequestFeature.RawTarget`. 7 skipped tests. [core-units.md](conformance/core-units.md#gaps) |
| <a id="gap-units-5"></a>`GAP-UNITS-5` | A `Response` that a component emits is dropped without a failure: the page answers 200 where upstream answers 500 (`ResponseSentError`) | The host renders with `streaming: false`, and Astro's `renderToString` only logs that it does not work with emitting responses. 1 skipped test. [core-units.md](conformance/core-units.md#gaps) |
| <a id="gap-units-6"></a>`GAP-UNITS-6` | `Astro.clientAddress` is the address of the connection: `X-Forwarded-For` is never read, so behind a proxy it is the address of the proxy | By design of the host. Use the forwarded headers middleware of ASP.NET Core, which updates `Connection.RemoteIpAddress`. 9 skipped tests. [core-units.md](conformance/core-units.md#gaps) |
| <a id="gap-units-7"></a>`GAP-UNITS-7` | Without `security.allowedDomains` the URL of a request keeps the `Host` that ASP.NET Core resolved, where the Node adapter replaces a `Host` it does not trust with `localhost` | By design of the host. Guard it with the `AllowedHosts` setting, or set `allowedDomains`. 6 skipped tests. [core-units.md](conformance/core-units.md#gaps) |

## Limits of the harness

- Builds go to a `dist*` folder inside the fixture, so an `outDir` outside the project root cannot be expressed. A one-off build of `vercel-static` with the output elsewhere gave a complete result; it is not automated.
- A variant can change the Astro config of a fixture, not its adapter options or its integrations, apart from the few that `support/integrations.mjs` knows. The `prerender-route` helper can force `prerender` to true only, and an array in a config override replaces the array of the fixture instead of merging with it.
- Test clients do not follow redirects, do not keep cookies and do not decompress, so each test sees exactly what the host sent. A host is started once per fixture and variant and shared by the classes that need it.
- The first run builds everything: the 106 fixtures and their variants make about 300 outputs, and a build takes about 1.4 seconds on the development machine. An output is built again when the newest file of its fixture, of `tests/conformance/support` or of the adapter's `dist` is newer than it, so a change of the adapter builds all of them again.
- The harness builds at most `max(2, cores / 4)` outputs at once. Every host stays up until the run ends, so a full run needs about 5 GB of memory in the test process. Set `CONFORMANCE_BUILD_SLOTS` to cap the builds on a machine that is short of memory.

## Running the suites

```powershell
pnpm install
dotnet test --project tests/AstroJint.Conformance.Tests/AstroJint.Conformance.Tests.csproj

# one area, one upstream file, or one class
dotnet test --project tests/AstroJint.Conformance.Tests/AstroJint.Conformance.Tests.csproj -- --filter-namespace AstroJint.Conformance.Vercel
dotnet test --project tests/AstroJint.Conformance.Tests/AstroJint.Conformance.Tests.csproj -- --filter-trait "Upstream=node/url.test.ts"
dotnet test --project tests/AstroJint.Conformance.Tests/AstroJint.Conformance.Tests.csproj -- --filter-class AstroJint.Conformance.Node.UrlTests
```

Skipped tests name their gap: `Skip = "GAP-VERCEL-1: ..."`. Search the tests for `GAP-` to see all of them.
