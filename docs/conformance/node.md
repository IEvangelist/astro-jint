# Node adapter tests

Upstream: `packages/integrations/node/test` (withastro/astro 7.3.8, commit 71e2b24b).

195 upstream tests: 123 ported, 56 adapted, 0 gaps, 16 not applicable. Every ported or adapted test runs against the real ASP.NET Core host over HTTP
(Kestrel on a loopback port) with the build output of an Astro fixture; the upstream display names are kept. Counted with `it(`, `it.skip(` and `test(`.

| Upstream file | `it`s | ported | adapted | gap | N/A | Ported as | Notes |
| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| `aborted-request.test.ts` | 1 | 0 | 1 | 0 | 0 | `Node/AbortedRequestTests.cs` | Raw socket against the host instead of a child process; the "output" it inspects is the host log |
| `api-route.test.ts` | 7 | 5 | 2 | 0 | 0 | `Node/ApiRouteTests.cs` | Large body scaled from 256 MiB to 8 MiB; "bail on streaming" cancels the render |
| `assets.test.ts` | 2 | 2 | 0 | 0 | 0 | `Node/AssetsTests.cs` | |
| `bad-urls.test.ts` | 2 | 2 | 0 | 0 | 0 | `Node/BadUrlsTests.cs` | |
| `build-format-file.test.ts` | 6 | 6 | 0 | 0 | 0 | `Node/BuildFormatFileTests.cs` | `file` and `preserve` formats |
| `encoded.test.ts` | 2 | 2 | 0 | 0 | 0 | `Node/EncodedPathnameTests.cs` | |
| `errors.test.ts` | 2 | 0 | 0 | 0 | 2 | n/a | Both tests are `skip: true` upstream (TODOs about errors in the middle of a stream) |
| `headers.test.ts` | 18 | 16 | 1 | 0 | 1 | `Node/HeadersTests.cs` | Headers are read off the wire; "sends several chunks" needs streaming (N/A); "sends a single chunk" is adapted |
| `image.test.ts` | 5 | 2 | 3 | 0 | 0 | `Node/ImageTests.cs` | No `sharp`: the image service passes pictures through, tests compare bytes; remote picture served by a loopback origin |
| `locals.test.ts` | 4 | 1 | 3 | 0 | 0 | `Node/LocalsTests.cs` | Locals come from `AstroOptions.LocalsFactory` instead of the Node handler's 4th argument |
| `node-middleware-listener-cleanup.test.ts` | 1 | 0 | 0 | 0 | 1 | n/a | Looks for a Node `MaxListenersExceededWarning`; `Node/StaticHeadersTests.cs` keeps the keep-alive scenario |
| `node-middleware.test.ts` | 10 | 2 | 8 | 0 | 0 | `Node/NodeMiddlewareTests.cs` | Express and Fastify variants fold into one run against the host |
| `prerender-404-500.test.ts` | 10 | 10 | 0 | 0 | 0 | `Node/Prerender404500Tests.cs` | The four `describe` blocks are fixture variants |
| `prerender.test.ts` | 33 | 31 | 0 | 0 | 2 | `Node/PrerenderTests.cs` | The two "Dev" tests start Astro's dev server (N/A) |
| `prerendered-error-page-fetch.test.ts` | 2 | 0 | 2 | 0 | 0 | `Node/PrerenderedErrorPageFetchTests.cs` | Cannot patch a global `fetch`; the engine has no outbound network, so a correct page proves the disk read |
| `preview-headers.test.ts` | 1 | 0 | 0 | 0 | 1 | n/a | `astro preview` option |
| `preview-host.test.ts` | 4 | 0 | 0 | 0 | 4 | n/a | `astro preview` option |
| `redirects.test.ts` | 6 | 6 | 0 | 0 | 0 | `Node/RedirectsTests.cs` | |
| `server-host.test.ts` | 3 | 0 | 0 | 0 | 3 | n/a | Unit test of the Node standalone server's `hostOptions()` |
| `session-false.test.ts` | 3 | 0 | 3 | 0 | 0 | `packages/astro-adapter-jint/test/session-config.test.ts` | Adapter configuration logic, covered by the adapter's own TypeScript tests |
| `sessions.test.ts` | 3 | 3 | 0 | 0 | 0 | `Node/SessionsTests.cs` | Sessions live in the host's `IDistributedCache` |
| `standalone-error.test.ts` | 1 | 0 | 0 | 0 | 1 | n/a | `EADDRINUSE` of the Node standalone server |
| `static-headers.test.ts` | 8 | 6 | 2 | 0 | 0 | `Node/StaticHeadersTests.cs` | `_headers.json` is replaced by the `astro-jint.json` manifest; the listener-cleanup test keeps the keep-alive scenario. The two upstream tests titled "CSP headers are added to the request" (one has an empty body) share one test |
| `trailing-slash.test.ts` | 38 | 14 | 24 | 0 | 0 | `Node/TrailingSlashTests.cs` | The "without base" blocks and the "base route" tests are adapted, see Differences |
| `units/resolve-client-dir.test.ts` | 1 | 0 | 0 | 0 | 1 | n/a | Locates `client` from the Node entry; the host takes `AstroOptions.DistPath` |
| `units/serve-static-path-traversal.test.ts` | 7 | 0 | 7 | 0 | 0 | `tests/AstroJint.AspNetCore.Tests/AstroStaticFileResolverTests.cs` | Unit tests of the host's own resolver, plus the traversal cases of `Node/WellKnownLocationsTests.cs` |
| `url.test.ts` | 11 | 11 | 0 | 0 | 0 | `Node/UrlTests.cs` | Real `Host` and `X-Forwarded-*` headers instead of fake requests |
| `well-known-locations.test.ts` | 4 | 4 | 0 | 0 | 0 | `Node/WellKnownLocationsTests.cs` | Traversal targets are sent raw (no client-side normalisation) and extended with encoded variants |
| **Total** | **195** | **123** | **56** | **0** | **16** | | |

## Differences

- **Streaming.** The host buffers request and response bodies, so the tests about chunking (`headers.test.ts`), a request body streamed in chunks and a response
  stream that the client cancels (`api-route.test.ts`) are not applicable or adapted: the body of `/hash` is 8 MiB instead of 256 MiB, as it must fit in
  `AstroOptions.MaxRequestBodyBytes` (10 MB by default), and a client that leaves cancels the render and frees the engine, since a script cannot observe the
  `cancel()` of a stream (the engine is thrown away).
- **Where headers are read.** Upstream compares what the adapter wrote to a mock response; here the headers are read from the HTTP response, so headers Kestrel adds
  to every response (`Date`, `Server`) are left out (`HeadersTests`).
- **Locals.** `AstroOptions.LocalsFactory` replaces the Node handler's `locals` argument (`LocalsTests`).
- **Middleware mode.** The ASP.NET Core host is both the standalone server and a piece of middleware, so the Express and Fastify variants of one check run once
  (`NodeMiddlewareTests`). Upstream's `next()` fall-through has no counterpart: Astro's fallback endpoint answers every request that no other endpoint takes.
- **`base` and static files.** The upstream fixture config sets `base: '/some-base'` and `loadFixture()` merges the inline config over it, so upstream's "without base"
  blocks do have a base and the Node adapter serves static files for paths outside of it. This host serves static files only inside `base`, so the "without base"
  variants here have no base, and the "base route" test requests the home page at its own URL (`TrailingSlashTests`).
- **Images.** `sharp` is a native module that Jint cannot load, so the image service passes pictures through (no resizing or format conversion). A remote picture needs
  `AstroOptions.FetchUrlFilter` to allow its origin and `image.domains` to list it, so the test serves it from a loopback origin (`ImageTests`).
- **Sessions.** The Node adapter keeps sessions in the file system; this host uses the application's `IDistributedCache`. A session written by one engine of the
  pool is read by another (`SessionsTests`). Astro marks its session cookie `Secure` in production builds, so a client must use https to send it back.
- **Static headers.** Upstream writes `dist/_headers.json` keyed by URL path; this adapter writes the headers of prerendered pages into `astro-jint.json` keyed by
  file, which is what a host that serves the files needs. Only the headers Astro derives from `security.csp` are recorded (`StaticHeadersTests`).
- **Prerendered error pages.** Upstream proves the adapter reads `404.html` and `500.html` from disk by patching the global `fetch`; the engine of this host has no
  outbound network at all, so the page rendering with the right content proves it (`PrerenderedErrorPageFetchTests`).
- **Host behaviours added to match what the Node adapter shows.** A backslash that the client wrote as one is folded into a slash before Astro sees the path
  (`/\example.com/press` is `//example.com/press`, as behind the Node adapter); a malformed `If-Match` on a static file fails with 412, where ASP.NET Core would
  ignore it; `Cache-Control: public, max-age=31536000, immutable` is only sent with 200, 206 and 304 answers, so a failed precondition never carries it.
- **Request URL.** Astro builds the URL from the `Host` and `X-Forwarded-*` headers the client sent, as the Node adapter does. A `Host` that
  `security.allowedDomains` does not list falls back to `localhost` and the port of the server, which is why the absolute `Location` of `Response.redirect` is
  compared with `http://localhost:{port}/destination` (`UrlTests`, `ApiRouteTests`).

## Gaps

None. Every portable upstream test passes.

## Not applicable

- **`astro preview`, `astro dev` and the Node standalone server** (`preview-headers`, `preview-host`, `server-host`, `standalone-error`, `units/resolve-client-dir`,
  the "Dev" block of `prerender`): options and processes of the Node adapter that have no counterpart in an ASP.NET Core host.
- **Node-only observables** (`node-middleware-listener-cleanup`, "sends several chunks"): a Node `MaxListenersExceededWarning` and chunked output of a streaming
  response.
- **Skipped upstream** (`errors`): both tests are `skip: true` in the upstream repository.

## Beyond upstream

The host adds behaviour that the Node adapter does not have, with tests of its own: `Host/BodyTests.cs`, `Host/ConsoleTests.cs`, `Host/HostContextTests.cs`,
`Host/ImageEndpointTests.cs`, `Host/SessionStorageTests.cs` and `Host/StaticHeadersHostTests.cs` in `tests/AstroJint.Conformance.Tests`, the unit tests in
`tests/AstroJint.AspNetCore.Tests` and the adapter tests in `packages/astro-adapter-jint/test`.
