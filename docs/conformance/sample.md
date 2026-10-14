# Sample site tests

Not derived from upstream. These tests use the adapter the way a site does: `samples/hybrid` is an Astro site that mixes prerendered and on-demand pages,
`samples/HybridHost` is the ASP.NET Core application around it, and `tests/AstroJint.Sample.Tests` starts that application in process
(`WebApplicationFactory<Program>`) and talks to it over HTTP. Building the host builds the site first (`pnpm --filter hybrid... build`, skipped with
`-p:SkipAstroSiteBuild=true`), so the tests always run against a real build output.

68 tests: 67 pass, 1 is skipped (`GAP-SAMPLE-1`).

The site keeps Astro's default `output: 'static'`. These pages are files written by `astro build`: `/`, `/about`, `/blog`, `/blog/<slug>` (from `getStaticPaths`),
`/products`, the 404 page, and `/news` (a configured redirect). These set `prerender = false` and are rendered for every request by the engine pool: `/time`, `/session`,
`/me`, `/contact`, `/diagnostics`, `/products/<id>`, `/api/time` and `/api/echo`. The clock on the home page is a server island (`server:defer`) in a prerendered page.

| Class | tests | Checks |
| --- | ---: | --- |
| `StaticPageTests` | 19 | Prerendered pages are served as the files `astro build` wrote, without the engine (a build-time value never changes, conditional requests give 304); `getStaticPaths` pages; the 404 page for unknown addresses; configured redirects; hashed assets are immutable; `public/` files; the build manifest and server bundle are not served |
| `DynamicPageTests` | 18 | On-demand pages are rendered per request (time, product price by currency query, 404 for unknown products); endpoints for every method; `Cache-Control` of on-demand responses; the page script is a static asset |
| `ContactFormTests` | 8 | A form that posts to its own page: validation, input shown again, redirect after the post, a cookie set by the endpoint and read back, a multipart post (curl style), an `Origin` that does not match is forbidden |
| `SessionAndIdentityTests` | 7 | `Astro.session` stored by the host (one session per browser, `HttpOnly` and `Secure` cookie); `Astro.locals` from `AstroOptions.LocalsFactory` (the ASP.NET Core user, a trace id per request); locals do not leak between requests |
| `HostIntegrationTests` | 13 | ASP.NET Core endpoints (`/healthz`, `/api/host`) answer next to the Astro routes and Astro's middleware does not see them; that middleware decorates every on-demand response and no file; `astro:env` (schema defaults, secrets from host configuration, public variables fixed at build time); `X-Forwarded-*` from a trusted proxy |
| `ServerIslandTests` | 3 | The prerendered page carries a fallback, the island is rendered on demand for every request, its encrypted props cannot be forged |

## Differences

Where a test looks odd, it is because of one of these:

- **Cookie security.** Astro marks its session cookie `Secure` in production builds, so the test client for a session talks https (`SampleSite.CreateClient(cookies: true, https: true)`); a browser
  or `curl` on plain http would not send the cookie back.
- **Public `astro:env` variables** of the server are inlined when the site is built; only `secret` variables can be set by the host's configuration (`Astro:Env:NAME`).
  `HostIntegrationTests` shows both.
- **`Cache-Control` order.** `HttpClient` parses and re-writes the header, so directives are compared as a set, not as the string the host sent.
- **Content types.** The host appends `; charset=utf-8` to text, JSON, JavaScript, XML and SVG files; on-demand pages answer with `text/html`, as the engine writes it.

## Gaps

- **`GAP-SAMPLE-1`: multipart form with an unquoted field name** (`ContactFormTests.AMultipartFormThatHttpClientWritesIsRead`, skipped). Jint 5.0.0-preview-2249 implements
  `Request.formData()` for multipart bodies, but its parser rejects a part whose `Content-Disposition` has an unquoted name (`name=email`), which is how .NET's
  `MultipartFormDataContent` writes it. Browsers and `curl` quote the name (`AMultipartFormIsReadLikeAUrlEncodedOne` passes), and url-encoded forms are not affected.
  Not reported to the Jint project yet. To do: try a newer preview, then report it.

## Not applicable

None; the suite is written for this adapter, so there is no upstream to compare with.
