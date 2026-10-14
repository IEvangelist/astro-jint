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

## Differences

- `ssr-request.test.ts`: requests carry `Host: example.com` instead of the absolute URL that `app.render` receives; assets are served by the
  static files layer (the test adapter serves `manifest.assets` itself). The tests also check the content type and the content of the
  CSS and script that the page links to, and that the redirect of the middleware points to `/`.
- `ssr-prerender.test.ts`: `Does not render static page` expects a 404 from the test adapter's app; the host serves `/static` from its
  file, so the port requests `/static?q=on-demand` and checks that the response is the file (ETag, empty `#searchparams`) and not a
  rendering. `includes prerendered pages in the asset manifest` checks that `client/static/index.html` exists and is served.
  `SSR routes still have inline styles in the SSR manifest` reads `app.manifest.routes`; the port checks that `/not-prerendered`
  still answers with the inline `<style>`. The `readFile` assertions on the prerendered page request it from the host instead, and
  `SSR entry chunk does not contain the prerender-only CSS string` reads `server/entry.mjs`, which is the server entry of this adapter too.
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

## Gaps

None so far.

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
