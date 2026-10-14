# Spike S2: running the Astro server bundle inside Jint

**Question.** Can Jint 5 (preview) load the Astro on-demand bundle produced in S1 and
answer real requests through its fetch-handler hosting API? What is missing?

**Setup.** `spikes/JintSpike` loads `dist-e3/server/entry.mjs` (single file) and
`dist-e1/server/entry.mjs` (chunked) into an `Engine` configured with
`UseWebApis()`, `UseFetch(f => f.UrlFilter = _ => false)`, `UseConsole(Console.Out)` and
`UseModules(<server dir>)`. It does `engine.Modules.ImportAsync("./entry.mjs")`,
`engine.WebApi.SetFetchHandler(ns.Get("default"))`, then sends
`HttpRequestMessage`s through `InvokeFetchHandlerAsync`. Run it with:

```powershell
$env:SPIKE = 'e3'; pnpm --filter spike-astro-app exec astro build   # or e1
dotnet run --project spikes/JintSpike -- spikes/astro-app/dist-e3/server
dotnet run --project spikes/JintSpike -- --probe            # JS compatibility probe
```

## Scenario results (e3 and e1 behave identically)

| Scenario | Result |
| --- | --- |
| `GET /time` (on-demand page) | 200, correct HTML, fresh timestamp per request |
| `GET /items/42?q=hello%20world` | 200, params, query string and `User-Agent` header all arrive |
| `GET /redirect` (`Astro.redirect`) | 302 with `Location: /time` |
| `GET /boom` (page throws) | 500, empty body (Astro's default without a `500.astro`) |
| `GET /crypto` | `crypto.randomUUID()` and `crypto.subtle.digest('SHA-256')` work, hash verified |
| `GET /delay` (`await setTimeout`) | 200, the 25 ms timer fires while the host pumps the engine |
| `POST /api/echo` (endpoint) | 201, custom header, body round-trips, **two `Set-Cookie` headers stay separate** |
| `POST /api/form` urlencoded + `Cookie` | `request.formData()` and `cookies.get()` work |
| `POST /api/form` multipart (browser style) | Works |
| `POST /api/form` multipart (.NET `MultipartFormDataContent`) | **500**, see Gap 5 |
| `GET /nope` | 404, Astro's default 404 page |
| `GET /api/echo` (no `GET` handler) | 404 after the buffering fix, see Gap 1 |
| `GET /` (a prerendered route) | 404: prerendered pages are not in the server bundle, as designed |
| `HEAD /time` | 200 **with a body**, see Gap 2 |

Chunked output (e1) works too: relative dynamic `import("./chunks/...")` resolves under
`UseModules(serverDir)` with no extra shims, because e1 already has no `node:*` imports.
The single-file bundle is still the recommended output (fewer files, one parse).

> **Update.** The adapter cannot guarantee a single file: a project that uses server
> islands (or any dynamic `import()` in on-demand code) still gets `server/entry.mjs` plus
> `server/chunks/*.mjs`. The host therefore no longer relies on `UseModules(<dir>)`. It
> loads the server bundle through `AstroModuleLoader`, which parses each file once for all
> engines in the pool, resolves only relative imports inside the server folder, and turns a
> refused import into a catchable rejection of `import()` (a static import that is refused
> fails engine creation with a message that names the module and the reason, without host
> paths). Jint does not turn a `ModuleResolutionException` thrown from `Resolve` into a
> rejection when the `import()` runs on a script's own stack, so refusals are carried by a
> sentinel `ResolvedSpecifier` and thrown as a `JavaScriptException` from `LoadModule`.

## Timing baseline (cold process, includes JIT warm-up; S3 measures properly)

| Step | Time |
| --- | --- |
| `new Engine(...)` with Web APIs | ~50-60 ms |
| Import the 625 KB unminified bundle (parse + evaluate) | ~380-430 ms |
| First request | 65-95 ms |
| Later requests | 1-10 ms |

## Gaps and decisions

1. **Responses must be buffered, and Astro re-wraps bodies.** Jint's hosting throws
   `InvalidOperationException: ... streaming response body ...` for stream-backed
   bodies. `createApp({ streaming: false })` is not enough: when Astro merges an error
   page (`mergeResponses` does `new Response(newResponse.body, ...)`, hit by a wrong-method
   endpoint call), the body becomes a stream. **Decision:** the server entry always
   returns `new Response(await response.arrayBuffer(), { status, statusText, headers })`
   when `response.body !== null`. This keeps multiple `Set-Cookie` values.
2. **`HEAD` responses carry a body.** Astro renders the GET body for HEAD. **Decision:**
   the ASP.NET Core bridge must not write the body for `HEAD`.
3. **Prerendered routes return the not-found page from the runtime.** **Decision:**
   the host serves `dist/client` first (clean URLs, `index.html`, redirects) and only
   falls through to Jint for everything else. Astro's `prerenderedErrorPageFetch`
   (prerendered `404.astro`/`500.astro`) needs a host function, planned in the host bridge.
4. **`error.stack` has no `Name: message` header and uses `\r\n` on Windows.** Astro
   logs `err.stack`, so errors show frames only and the message is lost. `stack` is a
   lazy accessor on `Error.prototype`, so a 12-line prelude restores the header (verified:
   `[ERROR] Error: boom` plus frames). `Jint.Options.Interop.BuildCallStackHandler` only
   formats individual frames, so it cannot do this. **Decision:** host runs the prelude
   on every engine before importing the bundle.
5. **Jint's multipart parser requires quoted `name=` / `filename=` values.** Round trips
   (`FormData` to `Request`) and browser/curl bodies parse; .NET's
   `MultipartFormDataContent` emits `name=field` and `filename*=utf-8''a.txt`, which fail
   with `TypeError: Failed to parse body as multipart/form-data`. Real clients are
   unaffected; our .NET tests must build multipart bodies with quoted parameters.
   Upstream issue candidate.
6. **Missing globals:** `process`, `Buffer`, `setImmediate`, `WebAssembly`, `global`,
   `window`. The e3 bundle needs none of them (every Node-ism is feature-detected:
   `"captureStackTrace" in Error`, `typeof process`, `typeof Buffer`). `UseNodeProcess()`
   is safe if a third-party package wants `process`: its `versions` is `{ "jint": ... }`
   with no `node` key, so devalue's `process.versions?.node` guard stays false. Note it
   reports `platform` of the host OS. Not enabled by default.
7. **Everything else a typical SSR page needs works:** `Intl` (DateTimeFormat,
   NumberFormat, PluralRules, RelativeTimeFormat, ListFormat, Segmenter), regex
   lookbehind/named groups/`\p{L}`, ES2023-2025 APIs (`toSorted`, `Object.groupBy`,
   `Promise.withResolvers`, `Array.fromAsync`, `Set.prototype.union`,
   `Uint8Array.fromBase64`), `structuredClone`, `URL`, `Response.json()`,
   `Headers.getSetCookie()`, `setTimeout`.
8. **The default time zone is the machine's** (`America/Chicago` here). **Decision:**
   expose `TimeZone` on the host options and default it to UTC for deterministic output.
9. **Outbound `fetch` is blocked** with `UrlFilter = _ => false` (rejects with
   `Failed to fetch`). The host will expose an opt-in allow-list.

## Verdict

Feasible. The Astro server runtime runs unmodified in Jint 5 behind
`InvokeFetchHandlerAsync`. The adapter must (a) buffer bodies in its server entry,
(b) ship the e3 build recipe, and the host must (c) serve prerendered files first,
(d) strip `HEAD` bodies, (e) apply the stack prelude and pin the time zone.
