# Netlify tests

Upstream: `packages/integrations/netlify/test` (withastro/astro 7.3.8, commit 71e2b24b).

Tests: `tests/AstroJint.Conformance.Tests/Netlify/` (namespace `AstroJint.Conformance.Netlify`), fixtures `tests/conformance/fixtures/netlify-*`.
`test-utils.ts` is a helper of upstream and has no tests.

| Upstream file | `it`s | ported | adapted | gap | N/A | Ported as | Notes |
| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| `development/primitives.test.ts` | 5 | 0 | 0 | 0 | 5 | n/a | `astro dev` with the Netlify Vite plugin: the dev server and the emulation of Netlify functions, edge functions and Image CDN |
| `functions/cache-provider.test.js` | 10 | 0 | 0 | 0 | 10 | n/a | Unit tests of the cache provider module of the Netlify adapter (`Netlify-CDN-Cache-Control`, `Netlify-Cache-Tag`); this adapter has no cache provider |
| `functions/cookies.test.ts` | 3 | 3 | 0 | 0 | 0 | `Netlify/Functions/CookiesTests.cs` | Requests go over HTTP to the host instead of to the generated function. Fixture `netlify-functions` |
| `functions/edge-middleware.test.ts` | 4 | 1 | 1 | 0 | 2 | `Netlify/Functions/EdgeMiddlewareTests.cs` | Only `middlewareMode: classic` applies; the 2 `middlewareMode: edge` tests are N/A (one is `it.skip` upstream too). "emits no edge function" is adapted: it checks the top level of the build and requests `/`. The build-time test is also run on `netlify-node-builtin`, the upstream fixture with `import https from 'node:https'`. Plus 1 skipped test for GAP-NETLIFY-1 (not an upstream `it`) |
| `functions/image-cdn.test.ts` | 13 | 0 | 0 | 0 | 13 | n/a | Netlify Image CDN: configuration written to `.netlify/v1/config.json` (`images.remote_images`), opt-out, `fit` mapped to the Netlify parameters |
| `functions/include-files.test.ts` | 7 | 0 | 0 | 0 | 7 | n/a | `includeFiles`/`excludeFiles` and the file tracing that packages the Netlify function |
| `functions/redirects.test.ts` | 4 | 2 | 2 | 0 | 0 | `Netlify/Functions/FunctionsRedirectsTests.cs` | "Creates a redirects file" is adapted (the responses of `/other` and `/other/`, a theory of 2 cases); "does not pass through 404 request" is adapted (loopback origin on a free port, `Host` header). Fixture `netlify-redirects` |
| `functions/session-false.test.ts` | 3 | 0 | 3 | 0 | 0 | `Netlify/Functions/SessionFalseTests.cs` | Upstream calls the `astro:config:setup` hook; these tests build sites and observe `Astro.session`, the bundle and the `IDistributedCache`. Fixtures `session-state` and `sessions` (existing) |
| `functions/sessions.test.ts` | 3 | 0 | 3 | 0 | 0 | `Netlify/Functions/SessionsTests.cs` | Sessions live in the `IDistributedCache` of the host instead of Netlify Blobs. Fixture `sessions` (existing) |
| `functions/skew-protection.test.ts` | 3 | 0 | 0 | 0 | 3 | n/a | Netlify skew protection: deploy id headers, `skew-protection.json`, `internalFetchHeaders` |
| `hosted/hosted.test.ts` | 3 | 0 | 2 | 0 | 1 | `Netlify/Hosted/HostedTests.cs` | Upstream runs these against a deployed site, outside its suite. "Image endpoint works" (fixture `image`) and "Server returns fresh content" (`netlify-functions`) run against the host; "passes context from edge middleware" is N/A (Netlify edge context, `Deno`) |
| `static/headers.test.ts` | 1 | 0 | 1 | 0 | 0 | `Netlify/Static/HeadersTests.cs` | Every file of `/_astro/` is requested and its `Cache-Control` is read, instead of the `_headers` rule. Plus 1 extra test (other static files). Fixture `netlify-redirects` |
| `static/image-missing-dimension.test.ts` | 1 | 0 | 1 | 0 | 0 | `Netlify/Static/ImageMissingDimensionTests.cs` | The failed `astro build` is the failure that the harness reports, with the name of the error in its output. Fixture `netlify-image-missing-dimension` |
| `static/redirects.test.ts` | 1 | 0 | 1 | 0 | 0 | `Netlify/Static/StaticRedirectsTests.cs` | The responses of the redirect routes instead of the `_redirects` snapshot (theory of 5 cases). Plus 1 extra test (the `/blog/*` redirect leads to a prerendered page). Fixture `netlify-redirects` |
| `static/static-headers.test.ts` | 2 | 0 | 2 | 0 | 0 | `Netlify/Static/StaticHeadersTests.cs` | The server island of the static site is requested from `server/`; the CSP the host sends with `index.html` is compared with the policy the build recorded in `astro-jint.json`. Fixture `netlify-static-headers` |
| **Total** | **63** | **6** | **16** | **0** | **41** | | |

## Differences

- **HTTP, not the generated function.** Upstream imports the function in `.netlify/v1/functions/ssr` (or `dist/`) and calls it with a `Request`; every test here sends the same request over HTTP to the host, which runs the build in Jint (all classes).
- **No Netlify output.** The build is `client/` (static files), `server/` (the bundle) and `astro-jint.json` (the manifest); there is no `_redirects`, `_headers`, `.netlify/v1/config.json`, function or edge function. Tests that snapshot those files check what they cause instead: redirect routes answer with their status (`StaticRedirectsTests`, `FunctionsRedirectsTests`), static files carry their `Cache-Control` (`HeadersTests`: `public, max-age=31536000, immutable` below `/_astro/`, `public, max-age=0, must-revalidate` for the others), the CSP of a prerendered page is the one in the manifest (`StaticHeadersTests`), and the build has no edge function (`EdgeMiddlewareTests`).
- **Sessions in `IDistributedCache`.** Netlify Blobs (and its mock `BlobsServer`) is replaced by the cache of the host; `devalue.parse` is replaced by a reader of the one shape the action answers with (`SessionsTests`, `SessionFalseTests`).
- **`Cache-Control` directive order.** `HttpClient` parses `Cache-Control` and writes the directives back in its own order (for example `public, must-revalidate, max-age=0`), so `HeadersTests` compares the sorted directives.
- **Loopback origin.** The origin that must not be reached (`does not pass through 404 request`) listens on a free port, not on 5678, and the request is addressed to it with the `Host` header (`FunctionsRedirectsTests`).
- **`middlewareMode`.** The middleware of this host always runs in its request pipeline, so only the classic mode applies (`EdgeMiddlewareTests`).
- **Hosted tests** run against the host that serves the fixtures. The hash of the built image is not known beforehand, so the test reads the address of the picture from the page (`HostedTests`).
- **Consolidated fixtures** (build budget): `netlify-functions` (the cookies and middleware fixtures of upstream, without the `node:https` import and the image page, plus the `time` page of the hosted project), `netlify-redirects` (the redirects fixtures of `functions/` and `static/`; stylesheets are never inlined, so that `/_astro/` has a file to ask for headers), `netlify-static-headers`, `netlify-node-builtin` (the middleware of upstream with its `node:https` import), `netlify-image-missing-dimension`. The existing fixtures `sessions` (same as upstream's apart from a log statement and `site`), `session-state` and `image` are reused. `functions/fixtures/includes`, `functions/fixtures/skew-protection` and the hosted project are not used (N/A tests).
- **Extra tests** (no upstream `it`, not counted above): `StaticRedirectsTests.SpreadRedirect_LeadsToThePrerenderedPage`, `HeadersTests.PagesAreRevalidated`, the `netlify-node-builtin` case of `EdgeMiddlewareTests.Classic_AppliesMiddlewareToStaticFilesAtBuildTime`, and the skipped test of GAP-NETLIFY-1.

## Gaps

### GAP-NETLIFY-1: `node:`-prefixed imports of Node built-ins in server code

- **Symptom.** A site whose middleware has `import https from 'node:https'` (the middleware fixture of upstream, also the hosted project) builds without errors, but no engine can be created at run time: the module loader of the host refuses the static import (`Only relative imports of files in the server bundle are supported in module 'entry.mjs': 'node:https'`) and every request that needs the engine answers 500. Prerendered files are still served.
- **Skipped test.** `EdgeMiddlewareTests.Classic_Middleware_CanImportNodePrefixedBuiltIns` ("middlewareMode: classic: middleware can import a node:-prefixed built-in module"): `GET /` on `netlify-node-builtin` must answer 200 with the title `Middleware` and `nodePrefixedImportExists` rendered as `true`. It is not an upstream `it`: the upstream tests only build that fixture and read a prerendered page, which passes here (the `netlify-node-builtin` case of `Classic_AppliesMiddlewareToStaticFilesAtBuildTime`). Verified without the skip: the request answers 500.
- **Suspected area.** The adapter build: Vite externalizes the Node built-in and leaves a static `import` of `node:https` in `server/entry.mjs`; the adapter could alias such modules to shims or fail the build with a clear message. The host (`AstroModuleLoader`) refuses non-relative imports by design.
- **Reproduce.** Remove the `Skip` and run `dotnet test --project tests/AstroJint.Conformance.Tests/AstroJint.Conformance.Tests.csproj -- --filter-class AstroJint.Conformance.Netlify.EdgeMiddlewareTests`, or build `tests/conformance/fixtures/netlify-node-builtin` and request `/`.

## Not applicable

- **Netlify Image CDN** (`functions/image-cdn.test.ts`, 13): the configuration of `images.remote_images` patterns in `.netlify/v1/config.json`, the opt-out and the mapping of `fit` to the Netlify parameters. This adapter replaces the image service by one that does not transform images and serves `/_image` with its own endpoint; what it accepts (`domains`, `remotePatterns`) is decided by the endpoint code of Astro and is covered by `Node/ImageTests.cs` and `Host/ImageEndpointTests.cs`.
- **Netlify skew protection** (`functions/skew-protection.test.ts`, 3): deploy id headers, `skew-protection.json` and the `internalFetchHeaders` of the manifest are a platform feature.
- **Packaging of the Netlify function** (`functions/include-files.test.ts`, 7): `includeFiles`/`excludeFiles` and the tracing of files into the function directory. The host runs one server bundle and traces nothing.
- **Netlify cache provider** (`functions/cache-provider.test.js`, 10): unit tests of the module that turns the cache options of a route into the CDN headers of Netlify. This adapter has no such module.
- **Dev server** (`development/primitives.test.ts`, 5): `astro dev` with the Netlify Vite plugin. The conformance suite serves built output with the host.
- **Netlify edge functions** (`functions/edge-middleware.test.ts`, 2: `middlewareMode: edge`, "emits an edge function" and the `it.skip`; `hosted/hosted.test.ts`, 1: "passes context from edge middleware", which needs the geo context and the `Deno` runtime of an edge function).
