# Core tests

Upstream: packages/astro/test (withastro/astro 7.3.8, commit 71e2b24b).

Every `it` of the adapter-facing core integration tests is classified as ported, adapted, gap or N/A. The tests build a fixture
with Astro's `testAdapter` and call `app.render(request, options)` in process; the ports build the same fixture with this
adapter and request it over HTTP from the ASP.NET Core host. Where an upstream test passes options to `app.render`, the port
expresses them through what an HTTP client or the host can say: `Host` and forwarded headers for the URL, `Cookie` headers for
`addCookieHeader`, the connection address (or `UseForwardedHeaders`) for `clientAddress`, `AstroOptions.LocalsFactory` for
`locals`. The test adapter serves `manifest.assets` itself and answers 404 for prerendered routes; the host serves both from
`dist/client` instead.

| Upstream file | `it`s | ported | adapted | gap | N/A | Ported as | Notes |
|---|---|---|---|---|---|---|---|
| `ssr-request.test.ts` | 6 | 6 | 0 | 0 | 0 | `Core\SsrRequestTests.cs` | Fixture `core-ssr-request`. |
| `ssr-prerender.test.ts` | 15 | 9 | 3 | 0 | 3 | `Core\SsrPrerenderTests.cs` | Fixture `core-ssr-prerender`; 2 of the `it`s are in a `describe.skip` upstream. |
| `ssr-dynamic.test.ts` | 6 | 5 | 1 | 0 | 0 | `Core\SsrDynamicTests.cs` | Fixture `core-ssr-dynamic` (the `injectRoute` integration is inline in its config). |
| `ssr-prerender-get-static-paths.test.ts` | 4 | 2 | 2 | 0 | 0 | `Core\SsrPrerenderGetStaticPathsTests.cs` | Fixture `core-ssr-prerender-get-static-paths`; the two describes (`server`, `static`) are the two Theory rows. |
| `ssr-script.test.ts` | 10 | 9 | 1 | 0 | 0 | `Core\SsrScriptTests.cs` | Fixture `core-ssr-script`; every `loadFixture()` variant is a JSON config variant. |
| `ssr-scripts.test.ts` | 2 | 1 | 1 | 0 | 0 | `Core\SsrScriptsTests.cs` | Fixture `core-ssr-scripts` (`@astrojs/preact`). |
| `ssr-assets.test.ts` | 1 | 0 | 1 | 0 | 0 | `Core\SsrAssetsTests.cs` | Fixture `core-ssr-assets`; the fixture is shared upstream with `logger.test.ts`, so it carries `src/logger.mjs`. |
| `middleware.test.ts` | 16 | 5 | 5 | 0 | 6 | `Core\MiddlewareTests.cs` | Fixtures `core-middleware` (`middleware space`), `core-middleware-no-user` and `core-middleware-full-ssr`. |
| `actions.test.ts` | 10 | 6 | 4 | 0 | 0 | `Core\ActionsTests.cs` | Fixtures `core-actions` (`actions`) and `core-actions-static` (`actions-static-prerender-false` plus the `subscribe` action). |
| `sessions.test.ts` | 8 | 8 | 0 | 0 | 0 | `Core\SessionsTests.cs` | Fixture `core-sessions` (`sessions`); 3 of the `it`s are the tests of `Node\SessionsTests` and are not repeated. |
| `session-false.test.ts` | 4 | 4 | 0 | 0 | 0 | `Core\SessionFalseTests.cs` | Fixture `core-session-config` with the JSON config `session: false`. |
| `session-tree-shake.test.ts` | 5 | 0 | 5 | 0 | 0 | `Core\SessionTreeShakeTests.cs` | Fixture `core-session-config`, shared with `session-false.test.ts`. |
| `server-islands.test.ts` | 19 | 10 | 7 | 0 | 2 | `Core\ServerIslandsTests.cs` | Fixtures `core-server-islands` (`server-islands/ssr`, with its content collection), `core-server-islands-hybrid` and `core-server-islands-custom-fetch`; the MDX content collection test was GAP-CORE-1 until dad60e1. |
| `csp-server-islands.test.ts` | 5 | 4 | 0 | 0 | 1 | `Core\CspServerIslandsTests.cs` | Fixtures `core-server-islands` and `core-server-islands-hybrid` with the JSON config `security.csp: true`; helpers `Core\ServerIslandRequests.cs` and `Core\CspDigest.cs`. |
| `csp.test.ts` | 8 | 7 | 1 | 0 | 0 | `Core\CspTests.cs` | Fixtures `core-csp` (the `kind` test is a JSON config variant), `core-csp-fonts` and `core-csp-adapter`. |
| `custom-fetch-error-pages.test.ts` | 4 | 0 | 4 | 0 | 0 | `Core\CustomFetchErrorPagesTests.cs` | Fixture `core-custom-fetch-error-pages`. |
| `error-page-cookies.test.ts` | 3 | 3 | 0 | 0 | 0 | `Core\ErrorPageCookiesTests.cs` | Fixture `core-error-page-cookies`. |
| `error-bad-js.test.ts` | 2 | 0 | 2 | 0 | 0 | `Core\ErrorBadJsTests.cs` | Fixture `core-error-bad-js`. |
| `env-secret.test.ts` | 4 | 3 | 1 | 0 | 0 | `Core\EnvSecretTests.cs` | Fixture `core-env-secret`; `AstroOptions.Env` plays the part of the test adapter's `env`. |
| `astro-envs.test.ts` | 13 | 10 | 3 | 0 | 0 | `Core\AstroEnvsTests.cs` | Fixture `core-astro-envs` (`@astrojs/vue`); the `SSR` describe is a JSON config variant (`output: 'server'`) with a runtime `Env`. |
| `client-address-node.test.ts` | 2 | 0 | 2 | 0 | 0 | `Core\ClientAddressNodeTests.cs` | Fixture `core-client-address`; the class has one more test than upstream (no forwarded headers middleware). |
| `request-signal.test.ts` | 3 | 0 | 1 | 2 | 0 | `Core\RequestSignalTests.cs` | Fixture `core-request-signal`; the 2 gaps are GAP-CORE-2 (skipped); the class has one more test than upstream (also skipped). |
| `cache-memory-query.test.ts` | 7 | 7 | 0 | 0 | 0 | `Core\CacheMemoryQueryTests.cs` | Fixture `core-cache-memory-query` holds both upstream fixtures (`query.include` is a switch of its config); one engine per site. |
| `live-loaders.test.ts` | 9 | 4 | 5 | 0 | 0 | `Core\LiveLoadersTests.cs` | Fixture `core-live-loaders`; the five `Dev` tests run against the production build. |
| `content-collections.test.ts` | 25 | 4 | 1 | 0 | 20 | `Core\ContentCollectionsTests.cs` | Fixtures `core-content-ssr-integration` (the `SSR integration` block) and `core-content with spaces in folder name`; 20 N/A (static builds with no adapter, below). |
| `content-collections-render.test.ts` | 10 | 10 | 0 | 0 | 0 | `Core\ContentCollectionsRenderTests.cs` | Fixture `core-content`: the `SSG` block is its static build, the `SSR` block the JSON config `output: 'server'`; one more test than upstream. |
| `serializeManifest.test.ts` | 5 | 5 | 0 | 0 | 0 | `Core\SerializeManifestTests.cs` | Fixtures `core-astro-manifest` (also holds the page of `astro-manifest-client-script`) and `core-astro-manifest-invalid`. |
| `logger.test.ts` | 2 | 0 | 2 | 0 | 0 | `Core\LoggerTests.cs` | Fixture `core-ssr-assets` (its `src/logger.mjs` logs a marker). |
| `runtime-logger.test.ts` | 6 | 0 | 6 | 0 | 0 | `Core\RuntimeLoggerTests.cs` | Fixture `core-runtime-logger`, which has one endpoint that upstream does not have (`src/pages/messages.json.ts`). |
| `ssr-large-array.test.ts` | 1 | 0 | 0 | 1 | 0 | `Core\SsrLargeArrayTests.cs` | Fixture `core-large-array` (`@astrojs/solid-js`); the gap is GAP-CORE-3 (skipped); the class has one more test than upstream (with the `worker` resolve condition, passes). |
| `ssr-partytown.test.ts` | 2 | 1 | 1 | 0 | 0 | `Core\SsrPartytownTests.cs` | Fixture `core-ssr-partytown` (`@astrojs/partytown`). |
| `ssr-renderers-static-vue.test.ts` | 2 | 1 | 1 | 0 | 0 | `Core\SsrRenderersStaticVueTests.cs` | Fixture `core-ssr-renderers-static-vue` (`@astrojs/vue`); one more test than upstream (the control with an on-demand page). |
| `before-hydration.test.ts` | 5 | 5 | 0 | 0 | 0 | `Core\BeforeHydrationTests.cs` | Fixture `core-before-hydration` (`@astrojs/preact`); the four builds of upstream are JSON config variants (`output`, `conformance.beforeHydration`). |
| `astro-basic.test.ts` | 15 | 7 | 5 | 3 | 0 | `Core\AstroBasicTests.cs` | Fixture `core-astro-basic` (`@astrojs/preact`, `@astrojs/mdx`; static, `output: 'server'` and `conformance.customPrerenderer` variants); the gaps are GAP-CORE-4 (skipped); three more tests than upstream (the same checks with `conformance.prerenderEntry`, pass). |
| `config-mode.test.ts` | 3 | 2 | 0 | 0 | 1 | `Core\ConfigModeTests.cs` | Fixture `core-astro-basic`; the N/A one is the build without an adapter. |
| `dev-request-url.test.ts` | 2 | 0 | 2 | 0 | 0 | `Core\DevRequestUrlTests.cs` | Fixture `core-astro-basic` (`output: 'server'`), with the `url` and `prerendered` pages of upstream's `dev-request-url` project. |
| `import-app.test.ts` | 1 | 0 | 1 | 0 | 0 | `Core\ImportAppTests.cs` | The import runs in the engine, from `/import-app`, an endpoint of `core-astro-basic` that is not upstream. |
| `ssr-adapter-build-config.test.ts` | 2 | 0 | 2 | 0 | 0 | `Core\SsrAdapterBuildConfigTests.cs` | Fixture `core-ssr-request`; `build.client` and `build.server` are set in the config instead of by an inline adapter. |
| `ssr-preview.test.ts` | 1 | 0 | 0 | 0 | 1 | none | Preview only. |
| `astro-preview-headers.test.ts` | 2 | 0 | 0 | 0 | 2 | none | Preview only. |
| `astro-preview-allowed-hosts.test.ts` | 5 | 0 | 0 | 0 | 5 | none | Preview only. |
| `core-image.test.ts` | 117 | 10 | 8 | 2 | 97 | `Core\CoreImageTests.cs` | Fixture `core-image-ssr` (the upstream SSR fixture, plus `src/custom-endpoint.ts` of `core-image` and a `public/pixel.svg` that upstream does not have; `base`, `trailingSlash`, the data-URL pattern and the custom endpoint are JSON config variants). The 20 `it`s with a test are the on-demand groups (`dev ssr`, `prod ssr`, `prod ssr - SVG format validation`, `trailing slash on the endpoint`, the two SSR tests of `support base option correctly`) and `custom endpoint`; 18 run and pass, and the two gaps, GAP-CORE-5 and GAP-CORE-6, are skipped; two more tests than upstream (pass). The 97 N/A are explained below. |
| `image-service-targets.test.ts` | 4 | 0 | 0 | 1 | 3 | `Core\ImageServiceTargetsTests.cs` | Fixture `core-image-ssr` (the `srcAlias` and `wrappingPrerenderer` switches). The `it` of the service that Vite resolves runs for two prerenderers upstream: the default one is ported and passes, the wrapping one is GAP-CORE-4 (skipped), so the `it` is counted as a gap. The other three need `image.service.{build,runtime}`, which Astro 7.3.8 does not implement, or a dev server. One more test than upstream (passes). |

Total: 43 files, 376 `it`s = 148 ported, 78 adapted, 9 gap, 141 N/A.

## Differences

- `ssr-request.test.ts`: requests carry `Host: example.com` instead of the absolute URL that `app.render` receives; assets are served by the
  static files layer (the test adapter serves `manifest.assets` itself). The tests also check the content type and the content of the
  CSS and script that the page links to, and that the redirect of the middleware points to `/`.
- `ssr-prerender.test.ts`: `Does not render static page` expects a 404 from the test adapter's app; the host serves `/static` from its
  file, so the port requests `/static?q=on-demand` and checks that the response is the file (ETag, empty `#searchparams`) and not a
  rendering. `includes prerendered pages in the asset manifest` checks that `client/static/index.html` exists and is served.
  `SSR routes still have inline styles in the SSR manifest` reads `app.manifest.routes`; the port checks that `/not-prerendered`
  still answers with the inline `<style>`. The `readFile` assertions on the prerendered page request it from the host instead, and
  `SSR entry chunk does not contain the prerender-only CSS string` reads `server/entry.mjs` upstream; the adapter splits the server build into
  `entry.mjs` and `chunks/*.mjs`, so the port reads every `.mjs` file under `server/`.
- `ssr-dynamic.test.ts`: `Public assets take priority` asserts that `app.match()` finds no route for `/favicon.ico`; the port requests the file
  and compares its bytes with `client/favicon.ico` (the `/[id]` route would answer with an HTML page). `Includes page styles`
  also fetches the linked stylesheet.
- `ssr-prerender-get-static-paths.test.ts`: `is only called once during build` asserts `true`; its only check is that the build does not
  throw (the `[...calledTwiceTest]` page throws when `getStaticPaths()` runs twice). The port keeps that check implicit in the fixture
  build and also requests the three pages the route returns (`/blog/a/`, `/blog/b/`, `/blog/c/`). `Astro.url sets the current pathname`
  requests `/blog/food/tacos/` from the host instead of reading the built file, and also asserts that `client/food/tacos/index.html` exists.
  The `output: 'static'` describe is a JSON config variant of the same fixture; an inline integration adds the Vite plugin that strips
  `export const prerender = true;` only when the resolved output is `static`.
- `ssr-script.test.ts`: the pages come from the host and the globs run on `dist`. The `script has correct path` tests also request the
  script (for an `assetsPrefix` they request the path of the CDN URL, where the host serves the same file). `with assetQueryParams` is
  adapted: the Jint adapter has no `client.assetQueryParams`, so the fixture config decorates the adapter's `setAdapter()` call to carry it
  (the test adapter's `extendAdapter` does the same upstream); the test also checks that the page script imports its chunk with
  `?dpl=test123` and that the host serves that URL.
- `ssr-scripts.test.ts`: `Are included in the manifest.assets so that an adapter can know to serve static` reads `app.manifest.assets`; the
  port requests the page and then the two scripts its `astro-island` names (`component-url`, `renderer-url`) from the host.
- `ssr-assets.test.ts`: `app.manifest.assets` is replaced by the content of `dist/client` (one file, a stylesheet) and a request for the
  stylesheet that the page links to.
- `middleware.test.ts`: the five tests of the dev-server describes (`Middleware in DEV mode — integration hooks`, `Integration hooks with no
  user middleware`) check that middleware registered with `addMiddleware()` (order `pre`, `post`, and an entrypoint given as a `URL`) runs.
  There is no dev server here, so they are adapted to the production build of the same integrations and pages; the host answers the same
  JSON (and the port also checks its content type). `should build the project without errors` also checks that the first request, not
  the build, imports the middleware (which throws on import, so that request answers 500). `app.render(request, { routeData: app.match(request) })`
  is a plain GET, `can set locals for prerendered pages to use` requests `/prerendered/` instead of reading `client/prerendered/index.html`,
  and the redirects of the encoded `/admin` paths are also checked for their `Location: /`.
- `actions.test.ts`: `new FormData()` is an empty `multipart/form-data` body and the `Referer` of the form posts is the address of the
  site. `followExpectedRedirect` is a helper that checks `REDIRECT_STATUS_CODES` and sends the `Location` back with the `ACTION_PAYLOAD`
  cookie as `name=value` (upstream forwards the whole `Set-Cookie` header, which the cookie parser reads the same way); the adapter always
  renders with `addCookieHeader: true`, so the `Set-Cookie` headers are on the wire. Three tests start a dev server upstream. `Does not
  process middleware cookie for prerendered routes` runs against the production build, where the prerendered page is a static file that
  the host serves without asking the app. `starts in dev and exposes action RPC routes` builds the same static-output project (one
  `prerender = false` page) with the adapter and posts to `/_actions/ping`; `devalue.parse()` becomes a read of the flat devalue array.
  `Works with adapter and all pages prerendered` uses a variant of that project (`conformance.allPrerendered`, an `astro:route:setup`
  hook that prerenders its only page) with the `subscribe` action, because the `actions` project cannot be built as static output
  (`invalid.astro` calls an action while it is prerendered). `Ignores action name for GET requests` asserts `assert.ok($('#user'))`
  upstream, true for any cheerio selection; the port asserts that the action did not run (200 and empty error elements).
- `sessions.test.ts`: the `fs` driver cannot run in Jint, so the fixture configures `session: { ttl: 20 }` and the adapter wires the session
  store of the host (`IDistributedCache`) in the driver's place. `can regenerate session cookies upon request`, `can save session data by
  value` and `can save and restore URLs in session data` run the same routes as `Node\SessionsTests` (fixture `sessions`), which proves
  them; the other five run here. `Origin: http://example.com` becomes the origin of the site, because the host builds the request URL from
  its `Host` header, and `devalue.parse()` becomes a read of the flat devalue array (`Core\Devalue.cs`).
- `session-false.test.ts`: the bundle check scans every `.mjs`, `.js` and `.cjs` file of the output directory (`Core\BuildOutput.cs`), which
  holds the one server module of this adapter. The routes are the ones of the `session-false` fixture, with `session: false` as a JSON override
  of the shared `core-session-config` fixture.
- `session-tree-shake.test.ts`: `session tree-shaking when no driver is wired` needs an adapter that wires no default driver, but this one wires the
  host's store unless `session: false` says otherwise; the three tests run with that setting, where `context.session` is undefined and the
  bundle has neither unstorage nor `AstroSession` (the same Astro mechanism, `provider-disabled`, applies to both). In
  `session runtime is retained when a driver is configured` the driver that the adapter wires stands in for `driver: 'fs'`; the tests check
  that `context.session` exists and that both stay in the bundle.
- `server-islands.test.ts`: `app.render(new Request('http://example.com/...'))` is an HTTP request. A `POST` body is the string that
  `JSON.stringify` gives upstream, so it travels as `text/plain` like the body of an upstream `Request`, and `origin: http://example.com`
  is the origin of the site, because the host builds the request URL from its `Host` header. `encryptString()` (WebCrypto) is `AesGcm`
  with the same wire format and the `ASTRO_KEY` of the upstream tests, which the fixture sets in its Astro config because the build
  runs in another process; the requests that must answer 200 show that the engine decrypts with that key. The five `dev` tests, the
  `custom fetch handler` test and the last `Hybrid mode` test run against the production build (`src/fetch.ts` is installed as the fetch
  handler by Astro's production entry; the endpoint of a static site answers once built). `supports fragments with named slots` requests
  `/fragment`, as upstream does twice, and also `/named-fragment`, the page the fixture has for it. `renders server island inside a slot in an MDX
    content collection entry` was a gap (GAP-CORE-1) until dad60e1 and passes.
- `csp-server-islands.test.ts`: the fixtures are the ones of `server-islands.test.ts` (`ssr` and `hybrid`) with `security.csp: true` as a
  JSON override, and the POST of the island carries the origin of the site. `SSR` / `omits the islands HTML` also checks (not asserted
  upstream) that the page answers with one `Content-Security-Policy` response header whose `script-src` holds the SHA-256 digest of
  every inline script of the page: with an on-demand page Astro 7.3.8 sends the policy as a header and the digests are computed in the
  engine. `Hybrid` / `build` reads the prerendered file through the host, which serves it unchanged. The `with no adapter` test is N/A.
- `csp.test.ts`: seven tests build with no adapter and read `<meta http-equiv="Content-Security-Policy">` from the files of the output
  directory; the port builds the same fixtures with this adapter (the pages stay prerendered, since no output is set) and requests each
  page from the host, which serves the file (the engine does not take part, as the files are the same). The `csp` fixture has no
  `react.astro`, `Text.jsx` or React integration, and `csp-adapter` has no `Text.jsx`: no test reads them. Upstream builds the fixture
  once per test; the tests here share one build, and `granular directives via the kind option` is the one JSON config variant
  (`scriptDirective` and `styleDirective`). `should return CSP header inside a hook` receives the routes' headers in the
  adapter's `setRouteToHeaders()` hook upstream; this adapter writes the same map into `astro-jint.json` (`staticHeaders`, one entry per
  prerendered page, with its file name) when its `staticHeaders` option is on, which the fixture sets, so it is adapted: the test
  expects the four prerendered pages in that list (not the on-demand `/ssr`), each with a `Content-Security-Policy` header, and also
  requests each of them: the host answers 200 with that header (not asserted upstream).
- `custom-fetch-error-pages.test.ts`: the tests pass a function as `prerenderedErrorPageFetch` to `app.render` (or none), count its calls and
  read the body it makes up. The adapter always passes its own function, which reads the page from `dist/client`, so a client cannot supply
  one. Each test requests the same URL (`/not-found`, `/pt/not-found`, `/causes-error`), asserts the same status, and asserts that the body is
  the prerendered page the URL names (`404`, `pt/404/index.html`, `500`), which shows that the right file was read; the call count and the
  made-up body are not observable. `falls back to global fetch...` expects a plain 404 because the global fetch cannot reach the page; the
  host finds the page, so the port asserts the status only. All four are adapted. `Node\PrerenderedErrorPageFetchTests` and
  `Node\Prerender404500Tests` prove the mechanism for the Node adapter.
- `error-page-cookies.test.ts`: `addCookieHeader: true` is what the adapter always passes, so `response.headers.getSetCookie()` is the
  `Set-Cookie` headers of the HTTP response.
- `error-bad-js.test.ts`: `Does not crash the dev server` runs against the production build, where a failing page answers 500 with an empty body
  and the error is in the host's log (the engine's console) instead of the page; it also checks that the host answers the next requests
  (upstream asserts the first text twice). `in nested components, does not crash server` expects the response stream to fail while it is read; the
  adapter renders to the end before it sends anything, so the failure is a 500 that holds no part of the page.
- `env-secret.test.ts`: the `env` option that the test adapter takes (how the runtime reads the environment) is `AstroOptions.Env` of the host,
  which the tests set with `configure` (one variant of the site per set of values). `works in dev` starts a dev server with
  `process.env.KNOWN_SECRET` set; the port requests the production build of the same fixture, built with `output: 'server'`, with the secret in
  `Env`. `fails if validateSecrets is enabled and secret is not set` expects the build to throw an error titled `Invalid environment
  variables.`; the harness throws an `InvalidOperationException` that holds the output of the failed build, where the CLI prints the name of the
  error (`EnvInvalidVariables`) and its message (`KNOWN_SECRET is missing`) but not its title, so the port asserts those two.
- `astro-envs.test.ts`: the `Build` tests set `BOOLEAN_VAR` and `NUMBER_VAR` in the test process; the build runs in another one, so the fixture's
  config sets them (`conformance.buildEnv`) and so it does for `SECRET_PLACE` in the `SSR` variant. The pages are read from the host under the
  base `/blog` (upstream reads the files of the output directory, and renders `/` in the SSR test). `does not include private env in client-side
  JS` lists only the root of the output directory upstream, where there is no script; the port reads every script of `dist/client` and requires
  that there are some. The `SSR` test also asserts that the runtime value (`SECRET_PLACE_SSR`, set through `Env`) is not in the page. The three
  `Development` tests run a dev server upstream; the port requests the production build, which prints the same elements (`#base-url`, `#env`,
  `#css`).
- `client-address-node.test.ts`: the Node adapter reads the first value of `x-forwarded-for` when the `Host` is one of
  `security.allowedDomains`. The host does not read that header: its client address is the address of the connection. An application that
  hosts it says which proxies it trusts with `UseForwardedHeaders`; the tests add it (trusting every proxy and the whole chain, so that the first
  value wins) in `configureApp`, and send `Host: localhost:<port>` and the same two header values as upstream. A third test, not in upstream,
  shows that without the middleware the header changes nothing (`127.0.0.1`).
- `request-signal.test.ts`: the two `Node request abort integration` tests emit `close` on a mock socket; the port disconnects the HTTP client.
  The state that the endpoints write lives in the module of the engine, so the tests use a site with one engine (`PoolSize = 1`, variant
  `one-engine`), which is busy until the host lets go of the request. Both are skipped as GAP-CORE-2. The `Dev server request abort signal`
  test only asserts that a request that is not aborted reports `aborted === false`, which the production build answers in the same way: adapted.
- `cache-memory-query.test.ts`: the two upstream fixtures (`cache-memory-query` and `cache-memory-query-include`) are one fixture whose config
  reads the switch `conformance.queryInclude` to give `memoryCache()` its `query.include` (`['page', 'sort']`), because `memoryCache()` is
  not JSON. Upstream requests `http://example.com/cached?...` through `app.render` and reads `X-Astro-Cache`; the tests send the same paths
  to the host. The memory cache lives in the engine, so every site runs one engine (`PoolSize = 1`, variant `one-engine`), as upstream's one
  in-process app does.
- `live-loaders.test.ts`: the fixture's config uses this adapter where upstream uses `@astrojs/node`. The five `Dev` tests start a dev server and
  request pages from it; their assertions do not depend on it, so they run against the production build (adapted). The four `SSR` tests build the
  fixture and call `app.render`, which is the host (ported). `deepEqual` is a JSON comparison of what `/api/` returns.
- `content-collections.test.ts`: the `SSR integration` block builds `content-ssr-integration` as an on-demand site and requests its pages from the
  host; the vite plugin of the test (`preventNodeBuiltinDependencyPlugin`) is in the fixture's config, so every build runs that check.
  `Throws deprecation error when using getEntryBySlug` expects a 500; the port also checks (not asserted upstream) that the host logged the
  deprecation error, so that it cannot pass because every request fails. `With spaces in path` is adapted: upstream builds the project with no
  adapter and asserts that nothing throws; the port builds it with the adapter, in a folder whose name has spaces, once prerendered and once
  with `output: 'server'` (the engine renders it), and requests the page. The other 20 `it`s are N/A (below). All four `SSR integration`
  tests were GAP-CORE-1 gaps until dad60e1.
- `content-collections-render.test.ts`: the `Build - SSG` block is the build with no `output` (every page prerendered into `dist/client`), whose files
  are requested from the host; the `Build - SSR` block is the JSON config `output: 'server'`. The fixture's config sets
  `build.inlineStylesheets: 'never'`, the default when upstream wrote the test. `De-duplicates CSS used both in layout and directly in target
  page` requests `/with-layout-prop/`, which the fixture has no route for, so upstream looks for duplicates in a 404 page; the port also asserts
  the 404 (not asserted upstream) and adds one test that is not upstream's, the same check on `/blog/promo/4-launch-week`, a page that exists
  and links a stylesheet. The five `Build - SSR` tests were GAP-CORE-1 gaps until dad60e1.
- `serializeManifest.test.ts`: `astro-manifest` and `astro-manifest-client-script` are one fixture, `core-astro-manifest` (the page of the
  second is `client-script.astro`; every build of the fixture builds it, which is what that test checks). What upstream reads with `readFile`
  from a static build is requested from the host, which serves the files of `dist/client`; the SSR tests request `/blog/server/` of a build with
  `output: 'server'`. The directory suffixes that the page prints (`/dist/serializeManifest-.../`) are the name of the output directory of the
  build under test (`dist` or `dist-<hash>`). `astro-manifest-invalid` expects an `AstroError` named `ServerOnlyModule`; the harness reports a
  failed build as an exception with the output of `astro build`, which holds the name of the error.
- `logger.test.ts`: upstream reads `app.logger.options.destination.__custom` / `.__relative` of the app that the test adapter returns, which
  nothing over HTTP can reach, and a destination whose `write()` is empty shows up nowhere else. The destinations of the port log a marker with
  `console.log` when the engine creates them, which the host writes to its log (category `AstroJint.Console`), and the tests assert that marker
  next to the 200 of `/`. The `src/logger.mjs` of the fixture (the `relative entrypoint`) has that line added, which upstream does not have.
  `custom entrypoint` writes its module outside the project (a folder of the test output), deletes it as soon as the build is done and starts the
  engine afterwards (`WarmUp = false`), so that the bundle cannot import it at runtime; upstream passes a `file:` URL object, the JSON config holds
  the absolute path.
- `runtime-logger.test.ts`: upstream reads the messages that `src/logger.mjs` records from the `globalThis` of the test process, which the built
  server shares. The engine has a `globalThis` of its own, so the fixture has one endpoint that upstream does not have,
  `src/pages/messages.json.ts`, which returns the recorded messages as JSON (`?clear` empties the record after the answer; the endpoint leaves
  out the `missing-in-handler` warning of its own request). The state is per engine, so the sites run one engine (`PoolSize = 1`); the four tests
  about the bootstrap each start a host of their own (a variant each) and make the first request themselves. The build runs at the default `info`
  log level, not upstream's `warn` (`logLevel` is an inline flag the harness cannot set); the tests select messages by their text, as upstream does.
- `ssr-large-array.test.ts`: the only `it` is a gap (`GAP-CORE-3`): a Solid island renders no HTML with the default configuration of this adapter.
  The test has the upstream assertions and one more, not asserted upstream: the `props` attribute of the island holds the whole prop (`[1, [...]]`,
  600 items, each with `foo` = `bar`). A second test, which upstream does not have, runs the same checks with
  `vite.ssr.resolve.conditions` set to `worker`, `module`, `browser` and `development|production` (a JSON config variant) and passes.
- `ssr-partytown.test.ts`: `The partytown scripts are in the manifest` looks for `/~partytown/partytown-sw.js` in `app.manifest.assets`, which nothing
  over HTTP can read. The manifest lists the files that the build puts in `dist/client` so that a request for one never reaches a route, so the port
  requests every file of `dist/client/~partytown` (the service worker among them) and compares the bytes with the file on disk.
- `ssr-renderers-static-vue.test.ts`: `does not include SSR renderers when only endpoints are dynamic` asserts `app.manifest.renderers` is empty, an
  in-process object. The manifest is part of the server bundle, where the list is written as `var renderers = [];` in the region of the
  `virtual:astro:renderers` module of `server/entry.mjs`, and the port asserts exactly that (a regular expression on the file) and that no file of the
  server build contains `createSSRApp`, the server renderer of Vue (`@astrojs/vue` as plain text is in the bundle either way, so it cannot be the
  negative marker). The page that makes the renderer necessary is the control, which upstream does not have: with the JSON config
  `conformance.onDemandPage` the fixture config sets `route.prerender = false` in `astro:route:setup`, and the same two checks then see the renderer
  (a non-empty list with `@astrojs/vue/server.js`, `createSSRApp` in `entry.mjs` and in the chunk `server-renderer_*.mjs`), `client/index.html` is
  gone and `GET /` renders the component in the engine.
- `before-hydration.test.ts`: upstream builds with no adapter for SSG (the page is read from the output) and with the test adapter for SSR. The four
  builds are JSON config variants of one fixture here: `output: 'static'` or `'server'`, each with and without `conformance.beforeHydration`, which
  makes the fixture config add the inline `@test/before-hydration` integration (`injectScript('before-hydration', "import '/src/scripts/global.js';")`).
  The SSG ones use this adapter with `output: 'static'`, where the page is prerendered into `dist/client`, and are requested over HTTP (ported). The
  chunk the `before-hydration-url` names is `/_astro/astro_scripts/before-hydration.js.<hash>.js`, a nested path under `_astro`; the tests check that
  the file exists in the output and that the host serves it as `text/javascript` (the second part is not asserted upstream). The tests where the
  integration is not used also check that the island is there (the page was not lost), which upstream does not.
- `astro-basic.test.ts`: the fixture `core-astro-basic` is the upstream project (`@astrojs/preact` and `@astrojs/mdx`, `vite.build.sourcemap: true`) plus three
  pages that other upstream tests or this port need (`url` and `prerendered`, copies of those of `dev-request-url`, and the `import-app` endpoint, see
  below). The `server` function of its config (a dev server option) is left out. Its config has two switches: `conformance.customPrerenderer` adds a copy
  of upstream's `test-prerenderer.ts` that writes the counts of its calls and the paths it rendered to `prerenderer-calls.json` (the build runs in another
  process, which a test cannot spy on), and `conformance.prerenderEntry` (GAP-CORE-4). The "build" tests read `/x/index.html` of the output upstream (built with
  no adapter); the port builds with this adapter and requests `/x/` from the host. The two "preview" tests request the host, which replaces the preview server.
  The two "development" tests run against `output: 'server'`, where the pages are on-demand: the dev server answers `/import-queries/raw/index.html`, but the
  production app answers 404 for it (the route is `/import-queries/raw`), so the request is `/import-queries/raw/`. `Renders markdown in utf-8 by default`
  keeps the assertion that the `Content-Type` has no `charset=utf-8`: the host sends Astro's `text/html` as it is. `server sourcemaps not included in
  output` lists the root of `dist`, where upstream lists the root of the site (a static site has no server build there), and adds that `dist/client` only
  has maps of client scripts and that `/entry.mjs.map` answers 404: with `sourcemap: true` the server bundle keeps its maps in `dist/server`
  (`entry.mjs.map` and `chunks/*.map`), which the host does not serve. The three "custom prerenderer" tests are the gap GAP-CORE-4; with `prerenderEntry` they
  pass (`setup`, `getStaticPaths` and `teardown` are called once, `render` 22 times, for the 22 paths of the 21 routes, `/` among them, and `GET /` is `Hello world!`).
- `config-mode.test.ts`: `loadTestAdapterApp()` and `app.render(new Request('http://example.com/'))` are `GET /` on the `output: 'server'` build, whose
  content type is exactly `text/html`, as upstream asserts. The static build reads `/index.html` from the output upstream; here it checks that
  `client/index.html` exists and that `GET /` answers with it.
- `dev-request-url.test.ts`: both tests run against the `output: 'server'` build of `core-astro-basic` instead of the dev server. The prerendered page is a
  file written at build time, so no query of a request can be in it.
- `import-app.test.ts`: the import has to work in the engine, so the fixture has an endpoint that is not upstream, `src/pages/import-app.ts`, which returns
  the export names of `await import('astro/app')` or a 500 with the error. The test also asserts that `App` and `BaseApp` are among them (upstream
  asserts `true === true` after the import).
- `ssr-adapter-build-config.test.ts`: upstream sets `build.client` and `build.server` from an inline adapter (Astro's adapter API) that also supplies
  its own server entry, the virtual module `@my-ssr`. The port sets the same two options in the user config (strings relative to `outDir`, so
  `./.root/client/` and `./.root/server/`) and checks the same files, `cars.json` and `entry.mjs`; this adapter records the directories in
  `astro-jint.json`, so the port also requests the public file and an on-demand page from the host, which only works if the host reads them from there. It
  also asserts that the default `client` and `server` directories were not created.
- `core-image.test.ts`: the upstream prod builds set `image.endpoint.entrypoint` to `astro/assets/endpoint/node`, which reads images from the file
  system; the port leaves the option alone, so the endpoint is the adapter's `astro-adapter-jint/image-endpoint` (it reads the local images through
  the host and shares Astro's `handleImageRequest`, so the status codes and bodies of the checks are Astro's). The image service is the adapter's
  passthrough one where upstream uses a stub (`testImageService()`), and neither transforms an image. The endpoint answers with the type it finds in
  the bytes, so the three `image/webp` assertions for `f=webp` on a JPEG (`dev ssr` `serves the image at /_image` and `returns HEAD method ok for /_image`,
  `prod ssr` `can load images from public dir`) are `image/jpeg` here. `dev ssr` runs the dev server with `base: 'some-base'` and asks for
  `/src/assets/penguin1.jpg?origWidth=...`; the port uses the production build with `base: '/blog'` (the base of the SSR test of the base group) and the
  `href` that the page gives its own image (`/blog/_astro/penguin1.<hash>.jpg`), and requests `/blog/api` because the production app answers only inside the
  base. `endpoint handle malformed requests` appends each bad path to the URL as it is and `new Request()` parses the URL (drops tabs, encodes control
  characters); the port serializes the query the same way and sends it over a raw socket, which does not touch the request target, and lists the two paths for
  which `isRemotePath()` is true (they start with a backslash, and get 403). `prerendered routes images are built` reads
  `client/prerender/index.html` and the image file from the output; the port reads the page from the host, checks the file in `dist/client` and also requests
  it. The first two `trailing slash on the endpoint` tests read `#local img` of the static `core-image` fixture on a dev server; here the page is the
  on-demand catch-all of `core-image-ssr`, built with `trailingSlash: 'always'` and `'never'` (the third one, with the unencoded `//` pattern, runs as upstream on a
  default build). `custom endpoint works` is the only `dev` test of an adapter-facing behaviour: the user's `image.endpoint.entrypoint` has to win over the
  adapter's (it only sets its own when the option is unset), so the port builds `core-image-ssr` with `./src/custom-endpoint.ts` (copied from `core-image`) and requests
  the page's own image URL.
- `image-service-targets.test.ts`: the port requests `/prerender` from the host where upstream reads `client/prerender/index.html`; the generated image is a
  file in `dist/client` in both. The Vite alias `~` is the `srcAlias` switch of the fixture config (a JSON config cannot carry the absolute path), the
  wrapping prerenderer is the `wrappingPrerenderer` switch, and a unique `cacheDir` per variant that the test deletes first gives the fresh asset cache that
  upstream gets from its own `cacheDir`. The non-upstream test requests the on-demand `/` page, to show that the engine also runs the aliased TypeScript
  service.

## Gaps

- `GAP-CORE-1` (fixed by dad60e1): a site that rendered a content collection entry (`render(entry)` of a Markdown or MDX entry) built without
  errors, but every request to it answered 500. The server build was a single `server/entry.mjs` (the adapter sets `ssr.target: 'webworker'`,
  for which Vite 8 builds the SSR environment with `codeSplitting: false` when it has one input) that still held
  `deserializeManifest("@@ASTRO_MANIFEST_REPLACE@@")`, so the engine failed while it was created (`TypeError: Invalid URL: undefined`, from
  `new URL(serializedManifest.rootDir)`) and the host logged an unhandled `PromiseRejectedException`. Astro injects the manifest
  (`virtual:astro:manifest`) and the links of the `?astroPropagatedAssets` module of the entry with `mutate(fileName, code)`, and
  `contentAssetsBuildPostHook` starts from the original `chunk.code`, so with both in one chunk its result replaced the manifest that
  `manifestBuildPostHook` had injected. dad60e1 (`Split the server build so content collection entries render`) makes the adapter set
  `environments.ssr.build.rolldownOptions.output.codeSplitting: true`: the server build is `server/entry.mjs` plus `server/chunks/*.mjs`
  and the manifest is injected. The tests that were skipped for it run and pass: `ServerIslandsTests.ContentCollectionEntry_IslandInSlot_IsInsideTheWrapper`,
  the four `SSR integration` tests of `ContentCollectionsTests` and the five `Build - SSR` tests of `ContentCollectionsRenderTests`. The fix has
  coverage of its own on main: the fixture `tests/conformance/fixtures/content-collection` and `Host/ContentCollectionHostTests.cs`.
- `GAP-CORE-2`: `request.signal` never aborts in the engine when the client disconnects. An endpoint that waits for the `abort` event of
  `request.signal` (`/stream` and `/sse` of `core-request-signal`) keeps its request pending; when the client closes the connection the host
  stops waiting (the cancellation token of the request goes to `Engine.WebApi.InvokeFetchHandlerAsync`) and retires the engine, so no `abort`
  listener runs: the console of the engine shows `started, aborted=false` for `/stream-log` and nothing after it, and the module state of the
  engine is gone (a site with one engine answers `{"aborted":false,"events":[]}` afterwards, where upstream reads `aborted: true` and
  `["started","aborted"]`). Skipped tests: `RequestSignalTests.Stream_RequestSignalAbortsWhenTheClientDisconnects`,
  `ServerSentEvents_RequestSignalAbortsWhenTheClientDisconnects` and the extra `AbortListener_RunsInTheEngineWhenTheClientDisconnects`.
  Suspected area: `AstroApp.RenderAsync` (`AstroApp.cs`: the token, and the slot that is discarded after a cancellation, design decision 5 of
  `docs/spikes/03-engine-pool-performance.md`) and the way the engine makes the `Request` (its `signal` is not aborted by that token). A user of
  `request.signal` (a streaming endpoint that must release a resource) never learns that the client left. Reproduce: run the three tests
  without their `Skip` (the constant `Gap` of the class).
- `GAP-CORE-3`: a Solid island renders no HTML on the server. The adapter sets `vite.ssr.target: 'webworker'`, and for that target Vite 8 resolves
  the packages of the server build with the client conditions (`module`, `browser` and `development|production`, no `worker`), so `solid-js/web`
  is its browser build, where `renderToString` and `renderToStringAsync` only log `... is not supported in the browser, returning undefined` (both
  lines are in the host log, category `AstroJint.Console`). The page answers 200 but the island is the text `undefined` followed by an `astro-island`
  with no server markup and without its slot content (the `h1` of `core-large-array` is missing; the 600 objects of the prop are in `props`). The
  `exports` of `solid-js` have a `worker` condition, listed before `browser`, that points at the server build, and the non-upstream test
  `LargeIslandProp_WithTheWorkerCondition_IsTheRightHtml` shows that the engine renders the page whole (viewport, icon, `main`, one island, the `h1`
  and the 600 objects) when `vite.ssr.resolve.conditions` has `worker`. A user of `@astrojs/solid-js` with this adapter has to set that option
  themselves today. Suspected area: the `vite.ssr` block of `astro:config:setup` in `packages/astro-adapter-jint/src/index.ts`, which could
  add the `worker` condition. Skipped test: `SsrLargeArrayTests.LargeIslandProp_ArrayBufferBody_IsTheRightHtml`. Reproduce: run it without its
  `Skip`.
- `GAP-CORE-4`: a user integration that calls `setPrerenderer` (in `astro:build:start`) cannot be built together with this adapter, with `output: 'static'`
  or `'server'`: the build stops at "Building server entrypoints..." with Vite's `rolldownOptions.input should not be an html file when building for SSR.
  Please specify a dedicated SSR entry.` (the harness raises it as an `InvalidOperationException` that holds the build output). In
  `core/build/vite-build-config.js` Astro gives the `prerender` environment `astro/entrypoints/prerender` as its input unless
  `!isLegacyAdapter(adapter) && settings.prerenderer`; the comment there says the entry is skipped "if an adapter with `entrypointResolution: 'self'` is
  used AND provides a custom prerenderer", but the code treats `'auto'`, which this adapter sets (`setAdapter({ entrypointResolution: 'auto', ... })` in
  `index.ts`), as a non-legacy adapter too, so the environment has no input and Vite falls back to an HTML one. The prerenderer that upstream registers
  delegates everything to the default one (`setPrerenderer((defaultPrerenderer) => ...)`), which changes nothing: any `setPrerenderer` call fails. Upstream
  does not meet it because `testAdapter()` is a legacy adapter (`serverEntrypoint: '@my-ssr'`, no `entrypointResolution`); the one with `'auto'` is
  `selfTestAdapter()`, which only `server-entry.test.ts` uses. Skipped tests: the three of `Astro custom prerenderer` in `AstroBasicTests` and
  `ImageServiceTargetsTests.WrappingPrerenderer_GeneratesImages_WithTheAliasedService` (the instance of the one `it` of `build image service resolved by Vite` whose
  integration wraps the prerenderer; it stops in the same place, and the default-prerenderer instance passes). Workaround
  (the non-upstream tests of the class, which pass): a Vite plugin that sets `build.rolldownOptions.input` of the `prerender` environment to
  `astro/entrypoints/prerender` (the `conformance.prerenderEntry` switch of `core-astro-basic`). Suspected area: either an Astro defect (the code and the
  comment disagree) or the `'auto'` of this adapter, which could give the environment its input in `astro:config:setup`. Reproduce: run the three tests
  without their `Skip` (the constant `GapCustomPrerenderer` of the class) or the wrapping-prerenderer test (the constant `WrappingPrerendererGap` of
  `ImageServiceTargetsTests`).
- `GAP-CORE-5`: the engine cannot `fetch()` a `data:` URL, so the image endpoint answers 500 for an image that is a data URL. With
  `image.remotePatterns: [{ protocol: 'data' }]`, `GET /_image?href=<data:image/svg+xml;base64,...>&f=svg` answers `500 Internal Server Error`, where Node
  answers 200 `image/svg+xml`. `fetch('data:...')` in the engine rejects with `TypeError: Failed to fetch` for a string, a `URL` and a `Request`, with
  `AstroOptions.FetchUrlFilter` allowing every URL (`new URL('data:...')` itself works): the fetch of Jint is HTTP(S) only (its assembly has no
  `data:` handling), where the Fetch specification decodes the data URL into a response. The PNG data URL with `f=svg` is answered 403 before any fetch
  (the format check), so that test passes. Every endpoint, route or component of a site that loads `data:` URLs with `fetch()` is affected, not only
  the image endpoint. Suspected area: the `fetch` of the engine (the Jint web APIs, which the host configures with `AstroOptions.FetchUrlFilter`): it
  could answer a `data:` URL itself. Skipped
  test: `CoreImageTests.Endpoint_AllowsSvgFormat_ForAnSvgDataUri`; the non-upstream `Endpoint_AllowsSvgFormat_ForAnSvgFile` shows that an SVG that the endpoint can
  read is served with `image/svg+xml`. Reproduce: run the skipped test without its `Skip`.
- `GAP-CORE-6` (minor): a local `href` with parent directory segments (`/../../../.env`, `/foo/../../../.env`, `/foo\..\..\.env`, `/%2e%2e/.env`) makes the
  image endpoint answer `404 Not Found` where Astro's endpoint answers `400 Invalid request`. `loadLocalImage` in
  `packages/astro-adapter-jint/src/image-endpoint.ts` resolves `'.' + href` against `http://local.invalid/`, and the URL parser drops the `..` segments
  that go above the root (`/../../../.env` becomes `/.env`), so the check for a path outside the output (`isParentDirectory` and `invalid-path` in Astro's
  Node endpoint) never sees it. Nothing outside `dist/client` can be read this way, because the host looks the path up in the client output only
  (`readClientBytes`): the non-upstream test `Endpoint_Answers404_ForParentDirectorySegments_WithoutServingAFileOutsideTheClientOutput` requests
  `/../astro-jint.json` and `/../server/entry.mjs`, two files that exist next to `client`, and gets 404 for both. Suspected area: `loadLocalImage`
  could resolve against a base with some depth (for example `http://local.invalid/x/`) and answer `invalid-path` when the result is not below it. Skipped test:
  `CoreImageTests.Endpoint_Rejects_LocalPathsWithParentDirectorySegments`. Reproduce: run it without its `Skip`.

## Not applicable

- `ssr-prerender.test.ts` / `prerendered routes have empty styles in the SSR manifest`: asserts on `app.manifest.routes[].styles`, the
  in-process manifest of the test adapter; the only effect of an empty list is a smaller bundle, which nothing over HTTP can observe
  (the neighbouring `SSR entry chunk` test covers the visible part).
- `ssr-prerender.test.ts` / `An integration can override the prerender flag` and `An integration can turn a normal page to a prerendered one`:
  both are in a `describe.skip` upstream (the comment there explains that the client build cannot change the prerender state), so they
  assert nothing.
- `middleware.test.ts` / `Middleware API in PROD mode, SSR` / `the integration should receive the path to the middleware`: builds with the test
  adapter's `adapterFeatures.middlewareMode: 'edge'` and its `setMiddlewareEntryPoint()` hook, the contract of an adapter that runs the
  middleware outside the server bundle (at a CDN edge). This adapter always runs the middleware in the engine next to the pages (it sets no
  `middlewareMode`), so there is no separate entry to receive and nothing for an HTTP client to observe.
- `middleware.test.ts` / `Middleware with tailwind` / `should correctly emit the tailwind CSS file`: a static build with no adapter that reads the CSS
  emitted by the Tailwind v4 Vite plugin from disk. It checks Vite/Tailwind output; nothing runs in the engine and the host only serves the file.
- `middleware.test.ts` / `Middleware HMR` (4 tests): they edit `src/middleware.js` and the modules it imports while the dev server runs and
  count executions through `x-test-executed` headers. Hot module replacement exists only in the dev server.
- `server-islands.test.ts` / `with no adapter` / `Errors during the build` and `Errors during dev`: build the fixture with `adapter: undefined`
  and expect Astro's `Cannot use server islands without an adapter.` build error, and its dev error page (`NoAdapterInstalledServerIslands`).
  Both are about a project that has no adapter, so there is no host and no request that could be made; the error is raised by Astro before
  this adapter or the host take part.
- `csp-server-islands.test.ts` / `Hybrid` / `build (no adapter)`: builds the hybrid fixture with `adapter: undefined` and expects the
  `NoAdapterInstalledServerIslands` error (it is in `server-islands.test.ts` as well). There is no adapter, host or request involved.
- `content-collections.test.ts` / `Query` (`Collection` 5, `Entry` 3 and `Scripts` 1), `Static paths integration` (3), `Base configuration` (3) and
  `Mutation` (1): each builds a static fixture with no adapter and reads what the build wrote (`json/*.json`, `*/index.html`, the scripts and CSS they
  link), which `getCollection()`, `getEntry()` and `render()` computed in Node at build time. The content layer does not run in the engine and the
  host would only serve the files, so a port would test the static file layer and not the runtime; the `SSR integration` block (ported) is the one
  that runs `astro:content` in the engine.
- `content-collections.test.ts` / `With legacy config`, `With empty markdown file` and `With numbers for IDs`: each expects `astro build` to fail with a
  message of the content layer (`Found legacy content config file in`, `**title**: Required`, `returned an entry with an invalid id`), and
  `With empty collections directory` builds a project with an empty collection and reads its static `index.html`. All of it is build-time
  validation of the content layer: no adapter, no runtime and no request.
- `config-mode.test.ts` / `output: 'server'` / `deploy config omitted` / `Throws during the build`: builds `output: 'server'` with no adapter and expects
  Astro's `without an adapter` error. Astro raises it before an adapter takes part, so there is no adapter, host or request (as for the `no adapter` tests of
  `server-islands.test.ts`).
- `ssr-preview.test.ts` / `preview server works`: starts and stops `astro preview` with the `previewEntrypoint` (`./preview.mjs`) that the test adapter
  declares. This adapter has no preview entrypoint (the host runs a built site), and the test asserts nothing about a response.
- `astro-preview-headers.test.ts` (2 tests): `server.headers` of the config, which the preview server of `astro preview` adds to the pages it serves (and not
  to a 404). The host does not read `server.headers`; the response headers of an ASP.NET Core application are those of its own middleware.
- `astro-preview-allowed-hosts.test.ts` (5 tests): the host-name allow-list of Vite's preview server (`vite.preview.allowedHosts`, `server.allowedHosts`,
  `true`), tested with raw requests with a custom `Host` header against `astro preview`. The counterpart in the host is the `AllowedHosts` setting
  (host filtering) of the ASP.NET Core application, which does not belong to this adapter.
- `core-image.test.ts` / `dev` (56 of its 57 `it`s; `custom endpoint` is ported): they start Astro's dev server on the static `core-image` project, which
  has no adapter, and assert what the dev server renders: the `basics` (22), `vite-isms` (3), `remote` (12), `markdown` (5), `getImage` (2),
  `content collections` (7), `regular img tag` (2) and `custom service` (3) tests read the HTML attributes of `<Image>`, `<Picture>` and `getImage()` with dev-server
  URLs (`/_image?href=/src/assets/...&origWidth=...`, `/@fs/...`, `?url` and `?raw` imports) and Vite's transform errors, and the `remote` ones also
  fetch `avatars.githubusercontent.com` and a local redirect server. The same components run in the engine for an on-demand page (the `#local img` of the
  ported catch-all page is one), but nothing that these `it`s assert is a behaviour of this adapter or host: the dev server is not part of the host and the
  URLs are not those of a production build. The groups that exercise the adapter are ported above.
- `core-image.test.ts` / `proper errors` (8): they start the dev server on `core-image-errors` and read the messages that Astro's internal logger receives (the
  `_logger` option of `startDevServer`, an internal API) for pages that misuse `getImage()` (no parameter, an undefined `src`, an ESM import passed directly),
  use an APNG (`<Image>`, `<Picture>` and a plain `<img>`) or reference a Markdown image that does not exist. The errors come from Astro's component code; a
  production host answers such a page with a 500 and logs the error to its own logger, and a missing Markdown image is an error of the Vite transform of the file,
  which a production build reports as a failing build. Nothing here depends on the adapter or the host.
- `core-image.test.ts` / `support base option correctly` (5 of its 7 `it`s; the two SSR ones are ported): builds the static `core-image-ssg` project with
  `base: '/blog'` and no adapter, and reads the `src` prefix in the generated `index.html` files (component, `getImage()`, direct import, Markdown, content
  collection). It checks Astro's build, and the host would only serve the files.
- `core-image.test.ts` / `build ssg` (21) and `custom service in build` (1): static builds with no adapter (and the `testImageService()` stub, which an adapter leaves alone)
  that read the files that Astro's build pipeline wrote to `dist`: the written images with their names and extensions, the cache entries in `node_modules/.astro`
  and the `quality`, `format`, `densities` and `srcset` outputs. Image generation at build time runs in Node and does not involve the adapter or the host,
  which would only serve the files; they check Astro's build, not a request.
- `core-image.test.ts` / `build ssg with 404 remote images` (2): a static build with no adapter that has to fetch a remote image that does not exist (and one that
  does) over the network.
- `core-image.test.ts` / `build data url` (4): a static build with no adapter that reads the files written to `dist` for `data:` image sources (short hash
  names, extensions, the written files and the inferred size). It uses the default sharp service, which this adapter replaces by its passthrough one (that
  writes no transformed files), so the outputs would not be the same.
- `image-service-targets.test.ts` / `separate build and runtime image services` (2) and `... in dev` (1, run for two kinds of page): they set
  `image.service` to `{ build, runtime }`. Astro 7.3.8 (the version this repository builds with) does not implement that option: its config schema drops the
  `build` and `runtime` keys, so the site is built with the default service and the pages get none of the attributes the tests look for (the upstream
  checkout is ahead of that release). The tests cannot pass with that Astro whatever the adapter, and the dev one also needs a dev server.
