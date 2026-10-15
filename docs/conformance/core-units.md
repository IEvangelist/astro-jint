# Core unit tests

Upstream: packages/astro/test/units (withastro/astro 7.3.8, commit 71e2b24b).

Tests: `tests/AstroJint.Conformance.Tests/CoreUnits/` (namespace `AstroJint.Conformance.CoreUnits`), fixtures `tests/conformance/fixtures/app-*`.
Run: `dotnet test --project tests/AstroJint.Conformance.Tests -c Release -- --filter-namespace AstroJint.Conformance.CoreUnits`.

The 89 upstream unit-test files below are the ones whose behavior a request to the host can reach, directly or through a probe
endpoint; every `it` in them is classified. Upstream builds a manifest by hand and calls `new App(manifest).render(request)` in
process. The ports build the same routes as real pages and endpoints in a fixture, let Astro build the fixture with the adapter, and
request them over HTTP from the ASP.NET Core host, so what `app.render()` takes as options is expressed through what a client or the
host can say. 1242 C# cases (24 skipped with a gap id) port the 1185 portable `it`s and add 57 cases of their own; display names
equal the upstream titles. `it(`, `it.skip(` and `test(` call sites are counted. The other 205 unit-test files (2382 `it`s) are
listed under Not applicable.

| Upstream file | `it`s | ported | adapted | gap | N/A | Ported as | Notes |
|---|---|---|---|---|---|---|---|
| `actions/action-error.test.ts` | 23 | 0 | 23 | 0 | 0 | `CoreUnits/ActionErrorTests.cs` | Client classes and helpers (`ActionError`, its codes, `ActionInputError`, `isActionError`, `isInputError`) run inside Jint through endpoint `action-api.json.ts` of `app-actions`; the unexported `ActionInputError` and code table are reached through `ActionError.fromJson()` and a copy of the table. |
| `actions/action-origin-check.test.ts` | 2 | 0 | 2 | 0 | 0 | `CoreUnits/ActionOriginCheckTests.cs` | Handler `src/fetch.ts` of `app-fetch` composes `actions()` before `middleware()` (pipeline `actions-before-middleware`); counter endpoint `/action-runs`; checks the origin of POST `/_actions/deleteAccount`. |
| `actions/action-path.test.ts` | 9 | 0 | 9 | 0 | 0 | `CoreUnits/ActionPathTests.cs` | The unexported `getActionQueryString` and `getActionPathFromString` are read through `actions.x.toString()` and `getActionPath(actions.x)`; base and trailing slash are site config; a name is encoded twice (`with%252Fslash`). |
| `actions/action-status.test.ts` | 3 | 0 | 3 | 0 | 0 | `CoreUnits/ActionStatusTests.cs` | Page `action-status.astro` prints the status; the host `LocalsFactory` supplies `_actionPayload` from header `x-action-result`. |
| `actions/actions-app.test.ts` | 30 | 0 | 30 | 0 | 0 | `CoreUnits/ActionsAppTests.cs` | RPC route `/_actions/[...path]`, table `src/actions/index.ts`, guard `src/middleware.ts`; extra builds with `base:'/base'`, `trailingSlash:'always'` and `security.actionBodySizeLimit:100`; origin check off; multipart written by hand; devalue compared as exact text. |
| `actions/actions-proxy.test.ts` | 12 | 0 | 10 | 0 | 2 | `CoreUnits/ActionsProxyTests.cs` | N/A 2: `returns symbol properties from the target` and `returns own properties from the target` need an `actionCallback` argument that only the client entry sets. |
| `actions/form-data-to-object.test.ts` | 25 | 0 | 25 | 0 | 0 | `CoreUnits/FormDataToObjectTests.cs` | How a posted form becomes action input, by the shape of the zod schema. |
| `actions/serialize.test.ts` | 19 | 0 | 19 | 0 | 0 | `CoreUnits/SerializeTests.cs` | `serializeActionResult` and `deserializeActionResult`: devalue for data, JSON for errors, 204 without data. |
| `app/astro-attrs.test.ts` | 3 | 3 | 0 | 0 | 0 | `CoreUnits/AstroAttrsTests.cs` | `addAttribute()` through pages `attributes`, `attributes/namespaced` and `attributes/namespaced-component` of `app-render`; AngleSharp reports a present boolean attribute as an empty string, and the raw check looks for `foobar=` rather than `data-foobar=`. |
| `app/astro-response.test.ts` | 3 | 3 | 0 | 0 | 0 | `CoreUnits/AstroResponseTests.cs` | Pages `not-found` and `not-found-custom` of `app-errors`: a 404 Response without a body gets Astro's default 404 page, a 404 set on `Astro.response` keeps the content. |
| `app/base-prefix-boundary.test.ts` | 6 | 0 | 6 | 0 | 0 | `CoreUnits/BasePrefixBoundaryTests.cs` | `base:'/app'` build of `app-pages`; `/appX/admin` must not be cut to `/admin`; the host always passes `routeData`, so every request carries it. |
| `app/csrf.test.ts` | 26 | 0 | 25 | 0 | 1 | `CoreUnits/CsrfTests.cs` | Real requests to the csrf endpoint (any method answers ok) of `app-pages`, Host `example.com`; the scheme cases and the `api.example.com` ones use the TLS site; no request body, as in upstream's mocks, and the host keeps the content headers of an empty body (GAP-UNITS-4, fixed). N/A 1: `allows any method on prerendered routes` (the host never renders a prerendered route on demand; a POST to one is an unknown route and still 403). |
| `app/dev-url-construction.test.ts` | 15 | 0 | 15 | 0 | 0 | `CoreUnits/DevUrlConstructionTests.cs` | Upstream's `buildDevUrl` copies the dev server's URL logic and the host has no dev mode, so the cases run against `app-fetch` variants (`AppForwarded.cs`); without allowed domains the host keeps the Host header, with allowed domains that do not match it uses localhost and the server port; `isHttps` is whether the connection of the site is TLS (an http site, or the TLS site of the harness for the one test that sets it). |
| `app/double-encoding-bypass.test.ts` | 19 | 0 | 18 | 1 | 0 | `CoreUnits/DoubleEncodingBypassTests.cs` | Catch-all `api/[...path]`, `api/admin/users`, a page and the middleware of `app-pages`, whose behavior (rewrite or passthrough, blocking by default, only below `/api`) comes from header `x-middleware-mode`; raw socket requests; body `admin only` proves the path was decoded and matched. Gap 1: GAP-UNITS-1. |
| `app/double-slash-bypass.test.ts` | 6 | 0 | 6 | 0 | 0 | `CoreUnits/DoubleSlashBypassTests.cs` | Real pages and the middleware of `app-pages` that guards `/admin` and `/dashboard`; raw socket requests. |
| `app/encoded-backslash-bypass.test.ts` | 6 | 0 | 6 | 0 | 0 | `CoreUnits/EncodedBackslashBypassTests.cs` | Dynamic route `users/[slug]` and a guard on `/users/admin`. |
| `app/error-pages.test.ts` | 11 | 0 | 11 | 0 | 0 | `CoreUnits/ErrorPagesTests.cs` | `app-pages` and `app-errors`: which error page answers when an endpoint fails, a method has no handler, no route matches, or a page returns 404 or throws. |
| `app/headers.test.ts` | 9 | 0 | 6 | 0 | 3 | `CoreUnits/HeadersTests.cs` | `createOutgoingHttpHeaders()` of `astro/app/node` is checked through endpoint `outgoing-headers` of `app-pages` (repeated values arrive joined with ", ", Set-Cookie arrives one line per value). N/A 3: `undefined input headers`, `null input headers`, `Empty Headers`. |
| `app/inert-script-dedup.test.ts` | 2 | 1 | 1 | 0 | 0 | `CoreUnits/AppInertScriptDedupTests.cs` | A client directive and the server island runtime, inside and outside `<template>`, emit their script both times. |
| `app/locals.test.ts` | 5 | 0 | 5 | 0 | 0 | `CoreUnits/LocalsTests.cs` | Locals come from `AstroOptions.LocalsFactory` (`x-foo` becomes `{foo}`) and reach pages, endpoints, the 404 and the 500 page. |
| `app/logger.test.ts` | 6 | 0 | 6 | 0 | 0 | `CoreUnits/LoggerTests.cs` | Destination through manifest `logger:{entrypoint}`; the host log category is `AstroJint.Console`; page `/logger/<message>` of `app-render`. |
| `app/malformed-uri.test.ts` | 3 | 0 | 3 | 0 | 0 | `CoreUnits/MalformedUriTests.cs` | `%C0%AF` over a raw socket must not answer 500. |
| `app/node.test.ts` | 79 | 0 | 55 | 14 | 10 | `CoreUnits/NodeTests.cs` | Upstream drives `createRequest` and `createRequestFromNodeRequest` with a mock `IncomingMessage`; the ports go through `AstroHttpBridge` with real requests. Gap 14: 8 GAP-UNITS-6 (first X-Forwarded-For address as client address) and 6 GAP-UNITS-7 (Host replaced by localhost when no domain is allowed). N/A 10: 5 request bodies that arrive as a Buffer, Uint8Array, ArrayBuffer, plain object or string, 2 abort signal (= GAP-CORE-2), 3 port 80, 443 or none (`AstroHttpBridgeTests` covers ports). |
| `app/response.test.ts` | 6 | 6 | 0 | 0 | 0 | `CoreUnits/ResponseTests.cs` | Pages `status-code` and `some-header` of `app-pages` plus the fixture's `404.astro`; status, status text and headers reach the client and `X-Astro-*` headers are removed; `Can set the statusText` reads the reason phrase (was GAP-UNITS-2, fixed). |
| `app/ssr-api-route.test.ts` | 10 | 0 | 10 | 0 | 0 | `CoreUnits/SsrApiRouteTests.cs` | Real routes of `app-pages` (`site:'https://mysite.dev/subsite/'`); multipart bodies written by hand with quoted names (`FormBodies.cs`); one extra test sends HttpClient multipart and is skipped (GAP-UNITS-3 = GAP-SAMPLE-1). |
| `app/trailing-slash.test.ts` | 32 | 0 | 32 | 0 | 0 | `CoreUnits/TrailingSlashTests.cs` | Real build of `app-pages`; Astro builds the pattern the mode asks for where upstream's patterns accept both; raw socket requests, because HttpClient would read `//target.example/path` as a network-path reference; repeated upstream titles are prefixed with their describe title. |
| `app/url-attribute-xss.test.ts` | 3 | 0 | 3 | 0 | 0 | `CoreUnits/UrlAttributeXssTests.cs` | Page `attributes/url-xss` of `app-render` gives four links; every start tag must hold one well-formed `href` and no attribute but `id` and `href`. |
| `cache/app-cache.test.ts` | 23 | 23 | 0 | 0 | 0 | `CoreUnits/CacheAppTests.cs` | Three builds of `app-cache`: the memory provider (`memoryCache()` of astro/config), `src/cdn-provider.mjs` (CDN-style, no `onRequest`) and none or disabled; Astro 7.3.8 takes route cache options from top-level `routeRules`; the memory cache lives in the one engine, so tests that expect a MISS use routes, hosts or queries no other test requests; the session test uses the host's session driver (a distributed cache) instead of unstorage memory. |
| `cookies/app-cookies.test.ts` | 12 | 0 | 11 | 0 | 1 | `CoreUnits/AppCookiesTests.cs` | Endpoint `cookie-ops` of `app-cookies` (steps get, has, set, delete, merge, headers, roundtrip, markResponseSent; `AppCookieOps`). N/A 1: `app.render can exclude the cookie from the Set-Cookie header` (the host always renders with `addCookieHeader:true`). |
| `cookies/delete.test.ts` | 6 | 0 | 6 | 0 | 0 | `CoreUnits/CookiesDeleteTests.cs` | Steps of `cookie-ops`; values are sent as percent-encoded JSON. |
| `cookies/error.test.ts` | 1 | 0 | 1 | 0 | 0 | `CoreUnits/CookiesErrorTests.cs` | `ResponseSentError` through step `markResponseSent`. |
| `cookies/get.test.ts` | 16 | 0 | 16 | 0 | 0 | `CoreUnits/CookiesGetTests.cs` | Decoders are chosen by name, because a function does not cross HTTP; 17 C# cases. |
| `cookies/has.test.ts` | 4 | 0 | 4 | 0 | 0 | `CoreUnits/CookiesHasTests.cs` | Steps of `cookie-ops`. |
| `cookies/merge.test.ts` | 8 | 0 | 8 | 0 | 0 | `CoreUnits/CookiesMergeTests.cs` | Target is `Astro.cookies` of the real request, source is a second `AstroCookies` that the endpoint creates. |
| `cookies/set.test.ts` | 11 | 0 | 11 | 0 | 0 | `CoreUnits/CookiesSetTests.cs` | Encoders are chosen by name: `base64` (written in the endpoint, the host has no Buffer), `spy` and `identity`. |
| `fetch/index.test.ts` | 79 | 0 | 68 | 6 | 5 | `CoreUnits/FetchIndexTests.cs` | Custom handler `src/fetch.ts` of `app-fetch` composes the `astro/fetch` functions (FetchState, trailingSlash, redirects, actions, middleware, pages, i18n, astro). Gap 6: 5 GAP-UNITS-1 (`trailingSlash() returns 400 instead of a redirect`, `middleware() returns 400 without running user middleware`, `redirects() returns 400`, `actions() returns 400`, `pages() returns 400`) and 1 GAP-UNITS-6 (`resolves clientAddress from X-Forwarded-For when host is trusted`). N/A 5: see Not applicable. |
| `fetch/provider-finalize.test.ts` | 1 | 0 | 1 | 0 | 0 | `CoreUnits/ProviderFinalizeTests.cs` | A rejecting finalize callback answers 500. |
| `fetch/rewrite-handler.test.ts` | 13 | 0 | 10 | 0 | 3 | `CoreUnits/RewriteHandlerTests.cs` | Page `/source` calls `Astro.rewrite()` and target `route-info.astro` shows `Astro.routePattern`, `Astro.url.pathname` and `Astro.params`, in variants of `app-routing`; `throws ForbiddenRewrite for SSR-to-prerender rewrite` is adapted: the built app holds no module for a prerendered route in the on-demand bundle, so `tryRewrite` stops with `Unexpectedly unable to find a component instance for route /static` and the answer is still 500. N/A 3: see Not applicable. |
| `i18n/i18n-app.test.ts` | 41 | 0 | 40 | 1 | 0 | `CoreUnits/I18nAppTests.cs` | Variants of `app-i18n` (no pages of its own; each variant lists its routes in its routes config, components in `src/routes`): prefixes (always, all but default), base, domains through `Host` and `X-Forwarded-Host`, protocol and `allowedDomains`, trailing slash. Gap 1: `rejects over-encoded paths on a mapped domain` answers 404 instead of 400 (GAP-UNITS-1). |
| `middleware/call-middleware.test.ts` | 13 | 0 | 13 | 0 | 0 | `CoreUnits/CallMiddlewareTests.cs` | Handlers `src/handlers/call-middleware.ts` run as real middleware, one URL each below `/call/`, and the response function is endpoint `src/pages/call/[...path].ts`; the three tests that expect a rejection (`MiddlewareNotAResponse`, `MiddlewareNoDataOrNextCalled`) expect 500 and read the error name from the fixture's `500.astro`. |
| `middleware/locals.test.ts` | 15 | 0 | 15 | 0 | 0 | `CoreUnits/MiddlewareLocalsTests.cs` | Adapted port: the helpers (`isLocalsSerializable`) run in the engine. |
| `middleware/middleware-app.test.ts` | 25 | 0 | 25 | 0 | 0 | `CoreUnits/MiddlewareAppTests.cs` | `app-middleware` with one real `src/middleware.ts`; handlers `src/handlers/middleware-app.ts` are chosen per URL and locals come from header `x-name` (`{name}` or `{}`); the 500 page prints `Astro.props.error.name`, so the tests assert more than upstream does. |
| `middleware/sequence.test.ts` | 10 | 0 | 10 | 0 | 0 | `CoreUnits/MiddlewareSequenceTests.cs` | Adapted port: `sequence()` runs in the engine. |
| `redirects/open-redirect.test.ts` | 12 | 0 | 12 | 0 | 0 | `CoreUnits/OpenRedirectTests.cs` | A Location is never protocol-relative (`//evil.com`), whether it comes from the destination or from a catch-all param that starts with a slash. |
| `redirects/redirect-page.test.ts` | 6 | 2 | 2 | 0 | 2 | `CoreUnits/RedirectPageTests.cs` | Four tests with an HTTP outcome against `app-redirects`: host redirects never carry a body, whatever `3xx.astro` holds. N/A 2: `renders the custom page as the redirect body with props` and `uses a 2 second delay for 302 redirects` (`experimental.redirectPage` is not in 7.3.8). |
| `redirects/render.test.ts` | 27 | 0 | 25 | 0 | 2 | `CoreUnits/RedirectsRenderTests.cs` | Values are read from real responses to `app-redirects` entries over raw requests; `redirectIsExternal` asserts Location (`http://example.com` becomes `http://example.com/`); `renderRedirect` applies encodeURI (a backtick becomes `%60`); `uses redirectRoute when available` sets `trailingSlash:'always'`. N/A 2: `falls back to "/" when no redirect is defined` and `returns / when redirect is undefined`. |
| `redirects/static-build.test.ts` | 18 | 2 | 2 | 1 | 13 | `CoreUnits/RedirectsStaticBuildTests.cs` | Five tests have an HTTP outcome: the middleware redirect and `Astro.redirect()` are ported, two tests on relative URLs in the redirect when `site` is set are adapted, and `Warns when used inside a component` is skipped (GAP-UNITS-5: 200 instead of 500). N/A 13: static meta-refresh output and `build.redirects`. |
| `redirects/template.test.ts` | 18 | 0 | 4 | 0 | 14 | `CoreUnits/RedirectsTemplateTests.cs` | 4 adapted through the trailing-slash handler (301 or 308 body of `/source` under `trailingSlash:'always'`). N/A 14: 4 need arguments the request path never passes and 10 test `hasMetaRefreshTo`, which 7.3.8 does not have. |
| `render/astro-global.test.ts` | 11 | 0 | 11 | 0 | 0 | `CoreUnits/AstroGlobalTests.cs` | Two builds of `app-render`: the default (no site, no base) and `site:'https://mysite.dev/subsite/'` with `base:'/blog'`, whose tests request `/blog/...`; middleware stashes `routePattern` and `isPrerendered` in locals; the pages are on demand. |
| `render/class-list-and-style.test.ts` | 25 | 2 | 23 | 0 | 0 | `CoreUnits/ClassListAndStyleTests.cs` | 23 adapted through probe endpoint `src/pages/probe/[name].ts`; 2 ported through pages `class-list/forwarded` and `class-list/style-forwarded`. |
| `render/client-address.test.ts` | 4 | 0 | 3 | 0 | 1 | `CoreUnits/ClientAddressTests.cs` | `Astro.clientAddress` of real requests. N/A 1: `Prerendered routes are not served at runtime (404)` (the host serves prerendered pages before the app is asked). |
| `render/context-helpers.test.ts` | 3 | 0 | 3 | 0 | 0 | `CoreUnits/ContextHelpersTests.cs` | The `Astro.session` and `Astro.csp` getters on real pages. |
| `render/custom-500.test.ts` | 3 | 3 | 0 | 0 | 0 | `CoreUnits/Custom500Tests.cs` | The fixture's one `500.astro` shows the error of `throws.astro` ('some error') and throws for any other, so `renders nothing if custom 500 throws` requests `throws-other.astro`; styles are linked `/_astro/*.css` files (`inlineStylesheets:'never'`) and the selectors are scoped, so the tests match declarations only. |
| `render/directives.test.ts` | 6 | 6 | 0 | 0 | 0 | `CoreUnits/DirectivesTests.cs` | Real pages `pages/directives/*.astro`; `<Fragment set:html>` in the slot of `WithSlot`. |
| `render/escape.test.ts` | 20 | 0 | 20 | 0 | 0 | `CoreUnits/EscapeTests.cs` | Through the probe endpoint of `app-render`. |
| `render/head-injection-app.test.ts` | 4 | 0 | 4 | 0 | 0 | `CoreUnits/HeadInjectionAppTests.cs` | Module `lib/head-injection.ts` and pages `pages/head-injection/*.astro`; the real pipeline propagates and injects head content. |
| `render/head.test.ts` | 4 | 0 | 4 | 0 | 0 | `CoreUnits/HeadTests.cs` | `isolates mutations to render instructions` goes through probe module `head`; three layout tests use pages `pages/head/{explicit,implicit,bare}.astro` and components `components/head/*`, because upstream stubs `headElements` while here a scoped style makes the real stylesheet link. |
| `render/html-primitives.test.ts` | 55 | 7 | 48 | 0 | 0 | `CoreUnits/HtmlPrimitivesTests.cs` | 7 ported through pages `primitives/spread`, `primitives/input`, `primitives/basic` and `primitives/order`; 48 adapted through the probe (the fragment-streaming test runs in full inside the probe); the two `drops malicious keys while keeping valid ones` titles are prefixed with their describe title. |
| `render/hydration.test.ts` | 14 | 0 | 14 | 0 | 0 | `CoreUnits/HydrationTests.cs` | The probe answers booleans and the symbol never leaves the engine; the client directive map has five names (load, idle, visible, media, only). |
| `render/inert-script-dedup.test.ts` | 4 | 0 | 4 | 0 | 0 | `CoreUnits/InertScriptDedupTests.cs` | Probe with a stub `SSRResult`. |
| `render/jsx-custom-elements.test.ts` | 5 | 0 | 5 | 0 | 0 | `CoreUnits/JsxCustomElementsTests.cs` | Probe with mock renderers. |
| `render/not-response.test.ts` | 1 | 1 | 0 | 0 | 0 | `CoreUnits/NotResponseTests.cs` | `not-response.astro` returns null: 500 with `OnlyResponseCanBeReturned`. |
| `render/render-context.test.ts` | 6 | 0 | 6 | 0 | 0 | `CoreUnits/RenderContextTests.cs` | Request-flow versions of the context tests; the two skipMiddleware tests post `?_action=countedForm` to `app-actions` on a single-engine host; fixture middleware throws on header `x-fail-middleware`, so a POST to `/form-result` renders `500.astro` without middleware; loggers are read from host log category `AstroJint.Console` ([WARN] and [ERROR]), `context.logger` through actions and `Astro.logger` through `/logger/page` and `/logger/hello` of `app-render`. |
| `render/rendering.test.ts` | 7 | 0 | 7 | 0 | 0 | `CoreUnits/RenderingTests.cs` | All 7 through probe module `rendering` (depth-first sync, deferred async, eager adjacent async siblings, blank fragments; timers included). |
| `render/slots.test.ts` | 19 | 19 | 0 | 0 | 0 | `CoreUnits/SlotsTests.cs` | Real pages `pages/slots/*.astro` and components `components/slots/*.astro`. |
| `render/ssr-elements.test.ts` | 20 | 0 | 20 | 0 | 0 | `CoreUnits/SsrElementsTests.cs` | Through the probe endpoint of `app-render`. |
| `render/streaming-rendering.test.ts` | 15 | 0 | 15 | 0 | 0 | `CoreUnits/StreamingRenderingTests.cs` | Pages in `src/pages/streaming-rendering` (7.3.8 has no `experimentalQueuedRendering`); the inline style and script of the head-content page are `is:inline`; `renders async components in parallel` compares start and end timestamps written into elements instead of a 90 ms wall time. |
| `render/streaming.test.ts` | 6 | 0 | 5 | 0 | 1 | `CoreUnits/StreamingTests.cs` | N/A 1: the chunked test (the host buffers the body and the Jint fetch hosting cannot read a stream-backed body). `sync sibling inside Fragment` is adapted through body order; `stays alive on failed component renders` requests `multiple-errors.astro` (a bare 500); the streaming-off page equals the others, because the host always renders with `streaming:false`. |
| `render/sync-expansion.test.ts` | 3 | 0 | 3 | 0 | 0 | `CoreUnits/SyncExpansionTests.cs` | Module `lib/sync-expansion.ts`; a partial page with six values compared exactly; the rejecting template page is requested twice on a one-engine host and answers two 500s that contain `boom`. |
| `render/tags.test.ts` | 3 | 0 | 3 | 0 | 0 | `CoreUnits/TagsTests.cs` | Through the probe endpoint of `app-render`. |
| `render/transitions.test.ts` | 32 | 0 | 32 | 0 | 0 | `CoreUnits/TransitionsTests.cs` | Through probe module `transitions`; several calls per test come back in one answer; the `_metadata.extraHead[0]` of the `renderTransition` result is parsed. |
| `routing/dev-routing.test.ts` | 5 | 0 | 5 | 0 | 0 | `CoreUnits/DevRoutingTests.cs` | Variant of `app-routing` with one more route (`/foo/bar`); characters are percent-encoded as a browser sends them; `does not normalize internal multiple slashes` gets 404 (`App.render` collapses duplicate slashes first); `redirects multiple leading slashes` redirects to `/` from the trailing-slash handler. |
| `routing/dynamic-route-collision.test.ts` | 3 | 0 | 3 | 0 | 0 | `CoreUnits/DynamicRouteCollisionTests.cs` | Injected routes and `match-info.astro`. |
| `routing/get-params.test.ts` | 22 | 3 | 15 | 0 | 4 | `CoreUnits/GetParamsTests.cs` | `params-json.astro` and `params-endpoint.ts`; 18 C# cases. N/A 4: the stringifyParams tests and `returns empty object when pattern does not match`. |
| `routing/resolved-pathname.test.ts` | 3 | 0 | 3 | 0 | 0 | `CoreUnits/ResolvedPathnameTests.cs` | Endpoints `/api/[category]` and `/api/[category]/[id]` (`trailingSlash:'never'` in the `build.format:'file'` variant shared with route-sanitization), answered by `match-endpoint.ts`; `App.render` keeps the `.html` suffix for endpoints (`books.html`, `42.html`, `1.html`) and twin pages `/pages/[category]` show upstream's values; the concurrency test sends its requests at once. |
| `routing/rewrite-app.test.ts` | 39 | 0 | 39 | 0 | 0 | `CoreUnits/RewriteAppTests.cs` | Each describe is a variant of `app-routing`; the Default variant has `checkOrigin:false` (the body-used test posts multipart); `target-probe.astro` sets `X-Rewritten-Method` and `X-Rewritten-Body`, middleware sets `X-Sequenced-*`; the host-provided-body tests send GET or HEAD with `Content-Length: 1` and `text/plain` (the host never gives Astro a body for GET or HEAD); `auth-sequence.ts` keeps the contextReroute flag in `context.locals`; the i18n manual routing group uses default routing (`pathname-prefix-other-locales`, `fallbackType:'rewrite'`); 41 C# cases. |
| `routing/rewrite-validation.test.ts` | 5 | 0 | 4 | 0 | 1 | `CoreUnits/RewriteValidationTests.cs` | In a built app ForbiddenRewrite answers `Unexpectedly unable to find a component instance for route /prerendered` (500 and the custom 500 page); middleware `src/middleware-library/rewrite-validation-sequence.ts`. N/A 1: `succeeds when manifest.serverLike is false (static-only build)`. |
| `routing/route-sanitization.test.ts` | 1 | 0 | 1 | 0 | 0 | `CoreUnits/RouteSanitizationTests.cs` | Injected route `/[...testSlashTrim]` answered by `match-info.astro`. |
| `routing/router-match.test.ts` | 19 | 0 | 16 | 0 | 3 | `CoreUnits/RouterMatchTests.cs` | The app asks the router with `allowWithoutBase:true`; duplicate slashes are collapsed and `////` redirects to `/`. N/A 3: the three `Router.matchAll` tests. |
| `routing/routing-app.test.ts` | 20 | 0 | 20 | 0 | 0 | `CoreUnits/RoutingAppTests.cs` | Custom 404 and 500 (also under a locale, with middleware locals), routes whose last segment is 404, redirects, base, `trailingSlash:'never'`, and an endpoint that answers 404. |
| `routing/routing-priority.test.ts` | 11 | 0 | 11 | 0 | 0 | `CoreUnits/RoutingPriorityTests.cs` | Static before dynamic before spread, per segment. |
| `routing/trailing-slash.test.ts` | 14 | 0 | 14 | 0 | 0 | `CoreUnits/RoutingTrailingSlashTests.cs` | `always` (a `/dot.json` never takes a slash) and `never`, with base; describe titles are kept. |
| `routing/virtual-routes.test.ts` | 1 | 0 | 1 | 0 | 0 | `CoreUnits/VirtualRoutesTests.cs` | A route added with `injectRoute()` really has `origin:'external'`, which is not observable, so the test asserts that the request is answered; shares its variant with routing-priority. |
| `server-islands/encryption.test.ts` | 16 | 0 | 16 | 0 | 0 | `CoreUnits/ServerIslandsEncryptionTests.cs` | All 16 through probe `src/lib/probes/encryption.ts` of `app-server-islands`, which imports `core/encryption.js` by path (AES-GCM and SHA-2 CSP digests). |
| `server-islands/endpoint.test.ts` | 20 | 11 | 9 | 0 | 0 | `CoreUnits/ServerIslandsEndpointTests.cs` | The real `/_server-islands/Island` of `app-server-islands`, in three builds (default; base with `trailingSlash:'always'`; CSP with a `serverIslandBodySizeLimit` of 200 bytes); the two Object.prototype-pollution tests go through `/probe/endpoint`; 23 C# cases. |
| `server-islands/server-islands-render.test.ts` | 26 | 0 | 26 | 0 | 0 | `CoreUnits/ServerIslandsRenderTests.cs` | A page render compared with probe `/probe/server-islands` (`src/lib/probes/server-islands.ts`). |
| `server-islands/shared-state.test.ts` | 4 | 0 | 3 | 0 | 1 | `CoreUnits/ServerIslandsSharedStateTests.cs` | 3 adapted through `names.astro` (Twin and AlsoTwin from twin-a, twin-b/Twin through TwinHolder). N/A 1: the build-time `ServerIslandsState`. |
| `sessions/astro-session.test.ts` | 35 | 0 | 34 | 0 | 1 | `CoreUnits/SessionsAstroSessionTests.cs` | Fixture `app-sessions` (endpoint `/api/ops`, `SessionOp`, `SessionSpyCache`, `SessionSeeds`), built with `session:{cookie:'test-session',ttl:60}`; devalue Map written by hand, Cookie header, a v4 UUID session id per test, Set-Cookie assertions; `should handle storage setItem failures` answers 500 and the host logs `The distributed cache failed to write session data.`. N/A 1: `should set secure to false in development`. |
| `sessions/session-false.test.ts` | 10 | 0 | 7 | 0 | 3 | `CoreUnits/SessionsSessionFalseTests.cs` | Builds `session:false` and `session:{ttl:60}`, endpoint `/api/has-session` and page `/user-provider`. N/A 3: `rejects other falsy values` and two symlinked provider path tests. |

Total: 89 files, 1256 `it`s = 92 ported, 1070 adapted, 23 gap, 71 N/A.

## Differences

- **Real builds instead of a hand-made manifest.** Upstream describes each page with `createComponent()` and a manifest object. Here
  the page is a real `.astro` file or endpoint in one of the thirteen `app-*` fixtures, Astro builds it with the adapter, and the
  test requests it from the host. A variant of a fixture (base, trailing slash, build format, i18n, size limits) is another build of
  the same sources with other Astro config.
- **Fixtures.** `app-render` (render modules through probe endpoints, slots, directives, streaming, head, the `Astro` global, logger,
  custom 500), `app-pages` (routing, middleware guards, csrf, response, error and security pages), `app-errors`, `app-fetch` (a custom
  `astro/fetch` handler and the forwarded-header variants), `app-actions`, `app-cookies`, `app-middleware`, `app-cache`, `app-i18n`,
  `app-sessions`, `app-routing`, `app-redirects` and `app-server-islands`.
- **Probe endpoints.** Modules that have no route of their own (escaping, hydration, head, transitions, rendering, tags, SSR elements,
  class list and style, HTML primitives, inert script dedup, JSX custom elements, server island encryption) are imported by a probe
  endpoint (`src/pages/probe/[name].ts` in `app-render`, `src/lib/probes/*.ts` in `app-server-islands`). The endpoint makes the calls
  the upstream test makes and answers with JSON; the assertions run in C#. What cannot be JSON (symbols, functions) is checked in the
  engine and answered as a boolean.
- **Functions do not cross HTTP.** Options that upstream passes as functions (cookie encoders and decoders, handlers, middleware) are
  chosen by name from a table in the fixture (`base64`, `spy`, `identity`), or by URL, one handler per URL below `/call/` or in
  `src/handlers/`.
- **What the host fixes.** Upstream gives `app.render()` its options one by one. The host always passes `routeData`, always renders
  with `addCookieHeader:true` and `streaming:false`, takes `clientAddress` from the connection (GAP-UNITS-6) and builds `locals` with
  `AstroOptions.LocalsFactory`. A test of an option the host fixes is N/A or adapted to the effect a client can see.
- **URL and host.** The URL is built from the scheme, the Host and the path that ASP.NET Core resolved. With `allowedDomains` an
  untrusted Host is replaced by `localhost` and the server port; without them the Host header stays (GAP-UNITS-7). Kestrel answers 400
  to Host headers it rejects (a path separator, a second port, a non-numeric port), a port above 65535 falls back to `localhost` and
  the server port, and HTTP/1.0 is the only way to send a request without Host.
- **Paths.** Kestrel decodes one percent-encoding level into `Request.Path` and the host does not encode it again (GAP-UNITS-1). A
  backslash that the client wrote as one is folded into a slash, as a URL parser does for http(s), while `%5C` stays a character of a
  segment. `App.render()` collapses duplicate slashes first. Requests with unusual targets go over a raw socket, because HttpClient
  normalizes them (`//target.example/path` is read as a network-path reference).
- **Prerendered files first.** The host serves a prerendered file before it asks the app, so a prerendered route is never rendered on
  demand; `.html` is stripped only for `build.format:'file'`. Tests that need the app to answer for a prerendered route are N/A or
  adapted.
- **Response.** The body is buffered; the host sends no framing headers of the JS response; a redirect leaves the host without a body
  whatever `3xx.astro` holds; `X-Astro-*` headers are removed; the reason phrase is carried (printable ASCII only).
- **Request body.** The host reads the whole body (`MaxRequestBodyBytes`, 10 MiB by default, then 413) and never gives Astro a body
  for GET or HEAD.
- **No dev mode.** `astro dev` is never involved, so dev-only behavior and the dev server's URL logic cannot be observed.
- **No Buffer.** The engine has no Node `Buffer`; fixture code that needs base64 writes it by hand.
- **Hand-written multipart.** `FormBodies.cs` writes multipart the way a browser does (quoted `name` and `filename`), because the
  strict `formData()` of Jint.WebApi rejects unquoted names (GAP-UNITS-3).
- **Markup parsing.** The tests parse HTML with AngleSharp where upstream uses cheerio; AngleSharp reports a present boolean
  attribute as an empty string, not as its name.
- **500 page.** The fixtures with a custom 500 page print `Astro.props.error.name`, so a test reads the error name from the page where
  upstream reads it from the rejection of `app.render()`.
- **Astro 7.3.8.** `astro/fetch` has no `notFound()`, and `experimental.redirectPage`, `hasMetaRefreshTo` and
  `experimentalQueuedRendering` are absent; tests of them are N/A.
- **Logging.** The host writes the JS console to log category `AstroJint.Console`; logger tests read it from there, and the
  destination is set through manifest `logger:{entrypoint}`.
- **Titles.** Display names equal the upstream titles; a title that repeats inside one file is prefixed with its describe title.

## Gaps

Each skipped test carries its gap id and the reason in its skip text (`grep GAP-UNITS`). 24 tests are skipped: the 23 `it`s counted as gap in the table, and the extra `SsrApiRouteTests` case of GAP-UNITS-3. Fixed:
GAP-UNITS-2 and GAP-UNITS-4.

- `GAP-UNITS-1`: over-encoded paths. Kestrel decodes one percent-encoding level into `Request.Path` and the host does not encode it
  again, so Astro sees one level less than the client sent. A path encoded eleven times reaches Astro as ten, which Astro still
  decodes (and answers 401, 404 or with a redirect), where Node rejects it with 400 because Astro decodes at most ten levels. A path
  encoded twelve times is rejected, and every skipped test has a passing twelve-level twin. Skipped (7): `DoubleEncodingBypassTests` 1,
  `FetchIndexTests` 5 (`trailingSlash() returns 400 instead of a redirect`, `middleware() returns 400 without running user
  middleware`, `redirects() returns 400`, `actions() returns 400`, `pages() returns 400`) and `I18nAppTests` 1 (`rejects over-encoded
  paths on a mapped domain`). Suspected area: `AstroHttpBridge.GetPath`, which takes the path from `HttpRequest.Path`; the raw target
  is available as `IHttpRequestFeature.RawTarget`.
- `GAP-UNITS-2` (fixed by `924feb7`): `Response.statusText` did not reach the client. It is now carried as the reason phrase
  (`IHttpResponseFeature.ReasonPhrase`, printable ASCII only). The `Can set the statusText` tests of `app/response.test.ts` run.
- `GAP-UNITS-3` (= GAP-SAMPLE-1 in `sample.md`): the strict multipart parser behind `formData()` in Jint.WebApi needs `name="..."`
  and rejects the unquoted names that HttpClient's `MultipartFormDataContent` writes. Skipped (1): the extra HttpClient multipart test
  of `SsrApiRouteTests`. The other tests post multipart written by hand (`FormBodies.cs`). The fix belongs in the parser of Jint.WebApi,
  or in the host normalizing the part headers.
- `GAP-UNITS-4` (fixed by `4024a9d`): a POST, PUT, PATCH or DELETE without a body reached Astro without any content, so the
  `Content-Type` of the request was lost with it: the origin check saw no form content type, and `request.formData()` on a form with
  no fields threw and failed the page with a 500. The request now always carries its (possibly empty) content, which keeps its
  content headers. The csrf tests send no body, as upstream's mocks do.
- `GAP-UNITS-5`: the host renders with `streaming:false`, and Astro's `renderToString` silently drops a Response that a component
  emits (it only logs that `renderToString doesn't work with emitting responses`) instead of throwing `ResponseSentError`. Skipped (1):
  `RedirectsStaticBuildTests` `Warns when used inside a component`, which answers 200 instead of 500.
- `GAP-UNITS-6`: the host always gives the address of the connection as the client address and never reads `X-Forwarded-For`. Behind
  a proxy, use ASP.NET Core's forwarded headers middleware: it updates `Connection.RemoteIpAddress`, the address the host reads.
  Skipped (9): `NodeTests` 8 (the first X-Forwarded-For address as `clientAddress`) and `FetchIndexTests` 1 (`resolves clientAddress
  from X-Forwarded-For when host is trusted`).
- `GAP-UNITS-7`: without allowed domains the host uses the Host that ASP.NET Core resolved, by design (see `AstroHttpBridge.BuildUrl`);
  upstream replaces an untrusted Host with localhost. Guard the host with the `AllowedHosts` setting, or configure `allowedDomains`.
  Skipped (6): `NodeTests` 6.

## Not applicable

Tests inside the 89 ported files (71):

- **Node plumbing that the host replaces with its own code** (15): `app/node.test.ts` 10 (5 on request bodies that arrive as a
  Buffer, Uint8Array, ArrayBuffer, plain object or string, 2 on the abort signal, which is GAP-CORE-2, and 3 on port 80, 443 or
  none; `AstroHttpBridgeTests` covers ports),
  `app/headers.test.ts` 3 (`undefined input headers`, `null input headers` and `Empty Headers` are inputs that a request never
  carries), `actions/actions-proxy.test.ts` 2 (the two tests of the proxy target need an `actionCallback` argument that only the client
  entry sets).
- **Options of `app.render()` that the host fixes** (4): `app/csrf.test.ts` 1 (`allows any method on prerendered routes`),
  `cookies/app-cookies.test.ts` 1 (`app.render can exclude the cookie from the Set-Cookie header`), `render/client-address.test.ts` 1
  (`Prerendered routes are not served at runtime (404)`), `routing/rewrite-validation.test.ts` 1 (`succeeds when manifest.serverLike
  is false (static-only build)`).
- **Internals that Astro 7.3.8 does not export, or that it does not have** (34): `fetch/index.test.ts` 5 (`throws when no ambient
  manifest is available`, `notFound() returns undefined when a route matches`, `notFound() serves a prerendered 404 page through
  prerenderedErrorPageFetch`, `marks all pipeline features as used even when first request is a redirect` and `warns on first access
  when session is not configured`; `astro/fetch` of 7.3.8 does not export `notFound()`), `fetch/rewrite-handler.test.ts` 3 (`sets
  isRewriting to true`, `invalidates cached contexts after rewrite`, `allows SSR-to-prerender rewrite for i18n fallback routes`),
  `redirects/redirect-page.test.ts` 2 (`experimental.redirectPage` is not in 7.3.8), `redirects/template.test.ts` 14 (4 on arguments
  that the request path never passes: `uses 2 second delay for 302 redirects`, `omits "from" text when not provided`, `handles
  external URLs in relative location`, `handles URL object for absoluteLocation`; 10 on `hasMetaRefreshTo`, which is absent),
  `redirects/render.test.ts` 2 (`falls back to "/" when no redirect is defined`, `returns / when redirect is undefined`),
  `routing/get-params.test.ts` 4 (the stringifyParams tests and `returns empty object when pattern does not match`),
  `routing/router-match.test.ts` 3 (the `Router.matchAll` tests), `server-islands/shared-state.test.ts` 1 (the build-time
  `ServerIslandsState`).
- **Build output rather than HTTP behavior** (13): `redirects/static-build.test.ts` 13 (6 on the static meta-refresh output,
  `returns null for a redirect route when build.redirects is false`, 3 on `InvalidRedirectDestination` and 3 on custom `3xx.astro`
  output; the adapter forces `build.redirects:false`).
- **Not observable over HTTP** (5): `render/streaming.test.ts` 1 (the chunked test), `sessions/astro-session.test.ts` 1 (`should set
  secure to false in development`; dev mode never runs), `sessions/session-false.test.ts` 3 (`rejects other falsy values` and the two
  symlinked provider path tests).

Upstream unit-test files that are not ported (205 files, 2382 `it`s). Five groups, by directory (`files/its`):

- **Tooling, build and dev-server plumbing with no host contract** (104 files, 942 `it`s): `build` 16/147, `cli` 9/45, `compile` 4/43,
  `config` 7/110, `content-collections` 5/36, `content-layer` 15/119, `dev` 15/194, `fs` 1/1, `preferences` 1/3, `preview` 1/3, `svg`
  1/5, `util` 4/52, `logger` 10/89 (the host logger is covered by the ported `app/logger.test.ts`), and the Vite plugins 15/95
  (`vite-plugin-astro-server` 2/7, `vite-plugin-astro` 3/16, `vite-plugin-environment` 3/11, `vite-plugin-head` 1/3,
  `vite-plugin-hmr-reload` 1/8, `vite-plugin-html` 3/44, `vite-plugin-markdown` 1/2, `vite-plugin-utils` 1/4). The tests import
  `dist/core/**` internals or build hand-made Vite mocks.
- **Image and font assets** (16 files, 327 `it`s): `assets` 16/327. Image transforms (sharp) are native and the rest is build
  tooling; the adapter swaps in a passthrough image service, and the run-time `/_image` behavior is covered by the `core-image.test.ts`
  rows of `core.md`.
- **Manifest, environment and build-time services** (20 files, 283 `it`s): `manifest` 3/36, `env` 1/33, `environment` 1/7, the
  top-level tests `environments`, `events`, `exports` and `remote-pattern` 4/31, `errors` 3/84, `integrations` 2/56, `render-scope`
  5/29, `runtime` 1/7 (`static-paths`). The defaults of the manifest (`allowedDomains: []`, `checkOrigin` only when requested and
  `buildOutput:'server'`, a 1 MB default for `actionBodySizeLimit` and `serverIslandBodySizeLimit`) are covered through the ported
  request tests.
- **Pure helpers of the bundle, or features whose HTTP-visible part is ported** (59 files, 805 `it`s): `i18n` 13/365 (pure helpers and
  middleware or SSG output; the HTTP-visible routing is covered by `i18n/i18n-app`, and `i18n-middleware` drives the app but is covered
  the same way), `render` 10/69 (`container*` is the Container API with no HTTP path, plus `head-propagation/*` and `paginate`),
  `routing` 20/133 (Router, manifest, route-table and HMR internals; `trailing-slash`, `routing-priority` and `router-match` are
  ported), `hono` 3/11 (the origin check is covered by `app/csrf`), `csp` 7/80 (generation is pure bundle code; the on-demand
  `Content-Security-Policy` header and the prerendered `<meta>` are covered by the `csp.test.ts` and `csp-server-islands.test.ts` rows
  of `core.md`, and `csp/rendering` drives the app but is covered the same way), `cache` 6/147 (the experimental route cache inside the
  bundle; its HTTP-visible subset is ported as `cache/app-cache`).
- **Internal or tooling-only single files** (6 files, 25 `it`s): `actions/actions-hmr` 1/2, `app/prerender-allowed-domains` 1/2 (it
  spies on the `request.headers` getter in FetchState; the observable rule is covered by the `Astro.url` tests),
  `fetch/features` 1/4, `middleware/middleware-hmr` 1/12, `redirects/environment-filtering` 1/4 (meta-refresh output of the build;
  the adapter forces `build.redirects:false`), `server-islands/vite-plugin-server-islands` 1/1.

104 + 16 + 20 + 59 + 6 = 205 files, 942 + 327 + 283 + 805 + 25 = 2382 `it`s. With the 89 ported files (1256 `it`s) the upstream
`test/units` directory holds 294 files and 3638 `it`s.

## Beyond upstream

1242 C# cases against 1185 portable `it`s leave 57 cases that have no upstream `it`: `app` 46 (`csrf` 26, `trailing-slash` 15,
`node` 4, `ssr-api-route` 1), `cookies/get` 1, `fetch/index` 5, `routing/rewrite-app` 2 and `server-islands/endpoint` 3. They are
further cases around the same rules; the only skipped one is the HttpClient multipart test of `ssr-api-route` (GAP-UNITS-3).
`MiddlewareAppTests` also asserts the error name that the fixture's 500 page prints, which upstream reads from the rejection.
