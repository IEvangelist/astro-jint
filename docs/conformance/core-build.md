# Core build and asset pipeline tests

Upstream: `packages/astro/test` (withastro/astro 7.3.8, commit 71e2b24b).

138 upstream tests: 107 ported, 13 adapted, 0 gaps, 18 not applicable. Every ported or adapted test runs against the real ASP.NET Core host over HTTP
(Kestrel on a loopback port) with the build output of an Astro fixture; the upstream display names are kept. Counted with `it(`, `it.skip(` and `test(`.

Tests: `tests/AstroJint.Conformance.Tests/CoreBuild/` (namespace `AstroJint.Conformance.CoreBuild`), fixtures `tests/conformance/fixtures/cb-*` and `_cb-underscore`, support code in
`tests/conformance/support/cb/`. The 20 files are the "build / asset-pipeline" group of the upstream inventory (section 3.3.2): what the build of Astro core writes (assets, CSS, fonts,
minified HTML, client-only islands, `astro/container`, incremental builds, the server entry) and what the host serves of it.

| Upstream file | `it`s | ported | adapted | gap | N/A | Ported as | Notes |
| --- | ---: | ---: | ---: | ---: | ---: | --- | --- |
| `asset-query-params.test.ts` | 7 | 7 | 0 | 0 | 0 | `CoreBuild/AssetQueryParamsTests.cs` | The adapter has no `assetQueryParams` option, so the fixtures wrap its integration (`support/cb`); the two identical inter-chunk describes share one test (6 tests for 7 `it`s); fixtures `cb-assets` and `cb-fonts` |
| `astro-assets-prefix.test.ts` | 23 | 23 | 0 | 0 | 0 | `CoreBuild/AstroAssetsPrefixTests.cs` | Fixture `cb-assets`, built once per `assetsPrefix` setting; the static and server describes are one Theory (the prerendered page and its on-demand twin under `/ssr/`); every linked asset is requested from the host |
| `astro-client-only.test.ts` | 10 | 10 | 0 | 0 | 0 | `CoreBuild/AstroClientOnlyTests.cs` | Fixture `cb-client-only`, built as static, as `output: 'server'` and as static with `base: '/blog'`; `.module.css` instead of `.module.scss` |
| `build-assets.test.ts` | 14 | 7 | 7 | 0 | 0 | `CoreBuild/BuildAssetsTests.cs` | The static describes are ported; the "server" describes (a test adapter with `buildOutput: 'static'`) are adapted to the on-demand twin of the page, because the adapter cannot declare it |
| `build-readonly-file.test.ts` | 2 | 2 | 0 | 0 | 0 | `CoreBuild/BuildReadonlyFileTests.cs` | Static and `output: 'server'` builds with the adapter; the test sets the read-only attribute itself; the file and the page are served |
| `container.test.ts` | 5 | 5 | 0 | 0 | 0 | `CoreBuild/ContainerTests.cs` | Fixture `cb-container`: endpoints that render React and Vue components with `astro/container` in Jint, requested over HTTP |
| `css-inline-stylesheets.test.ts` | 8 | 8 | 0 | 0 | 0 | `CoreBuild/CssInlineStylesheetsTests.cs` | Theories over `inlineStylesheets` never, auto and always, each in a static and an `output: 'server'` build (fixture `cb-css-inline`); "auto in server output" was GAP-BUILD-1 |
| `css-order.test.ts` | 4 | 4 | 0 | 0 | 0 | `CoreBuild/CssOrderTests.cs` | Fixture `cb-css-order`, which also has the `transparent` page of `css-order-layout`; the parity test builds with `inlineStylesheets: 'never'` and was GAP-BUILD-1 |
| `css-pure-chunk-query-params.test.ts` | 2 | 2 | 0 | 0 | 0 | `CoreBuild/CssPureChunkQueryParamsTests.cs` | The Vue island of `cb-container`, built with the wrapped adapter; every import of a client script is requested from the host the way a browser resolves it |
| `css-server-output-dedup.test.ts` | 3 | 3 | 0 | 0 | 0 | `CoreBuild/CssServerOutputDedupTests.cs` | Fixture `cb-css-dedup`; all three tests were GAP-BUILD-1 |
| `dev-css-page-boundary.test.ts` | 2 | 0 | 0 | 0 | 2 | n/a | Both tests start the Astro dev server |
| `featuresSupport.test.ts` | 2 | 0 | 1 | 0 | 1 | `CoreBuild/FeaturesSupportTests.cs` | The first test is adapted to the `i18nDomains` message that Astro logs for this adapter; the second (`build.split`) is N/A, the option is gone |
| `fonts.test.ts` | 20 | 11 | 1 | 0 | 8 | `CoreBuild/FontsTests.cs` | Fixture `cb-fonts` with `fontProviders.local()` (offline); the 8 tests of the dev groups are N/A; the ssr "Exposes buffer" test is adapted (loopback `FetchUrlFilter`) |
| `image-deletion.test.ts` | 15 | 11 | 3 | 0 | 1 | `CoreBuild/ImageDeletionTests.cs` | Fixtures `cb-image-deletion` and `cb-image-deletion-ssr`; the three tests of the wrapping prerenderer are adapted; "generates images before teardown" is N/A |
| `incremental-build-headers.test.ts` | 1 | 0 | 1 | 0 | 0 | `CoreBuild/IncrementalBuildHeadersTests.cs` | The policy of the restored page is read from the HTTP response (the adapter writes it into `astro-jint.json`) instead of from the callback of the test adapter |
| `incremental-build-islands.test.ts` | 1 | 1 | 0 | 0 | 0 | `CoreBuild/IncrementalBuildIslandsTests.cs` | Two builds into one cache; the output file is requested from the host, and so is the island of the page that was rendered again |
| `minification-html.test.ts` | 11 | 10 | 0 | 0 | 1 | `CoreBuild/MinificationHtmlTests.cs` | Fixture `cb-minify` with `compressHTML` and `output` variants; every check of a prerendered page also runs on the on-demand page; "in DEV environment" is N/A |
| `resolved-vite-output-directories.test.ts` | 1 | 0 | 0 | 0 | 1 | n/a | Needs a newer Astro core than the published 7.3.8, see "Not applicable" |
| `server-entry.test.ts` | 6 | 2 | 0 | 0 | 4 | `CoreBuild/ServerEntryTests.cs` | The two scenarios in which the adapter hands Astro its own `serverEntrypoint` are ported; the legacy adapter test and the three `rolldownInput` tests are N/A |
| `underscore-in-folder-name.test.ts` | 1 | 1 | 0 | 0 | 0 | `CoreBuild/UnderscoreInFolderNameTests.cs` | Fixture `_cb-underscore`; the fake library sits next to the config |
| **Total** | **138** | **107** | **13** | **0** | **18** | | |

The upstream fixtures are consolidated into 15, because every build costs time: `cb-assets` (stylesheets, images, Markdown and MDX pages and a React island, each page also on demand under
`/ssr/`; it replaces the fixtures of `astro-assets`, `astro-assets-prefix`, `multiple-jsx-renderers` and `asset-query-params-chunks`), `cb-client-only`, `cb-container` (which also holds the Vue
island of `css-pure-chunk-query-params`), `cb-css-dedup`, `cb-css-inline`, `cb-css-order`, `cb-fonts`, `cb-image-deletion`, `cb-image-deletion-ssr`, `cb-incremental-headers`,
`cb-incremental-islands`, `cb-minify`, `cb-readonly-file`, `cb-server-entry` and `_cb-underscore`. The framework integrations (React, Vue, Svelte, MDX, Markdoc) are only installed in the
fixtures that need them. A config variant (`build.assets`, `inlineStylesheets`, `base`, `assetsPrefix`, `compressHTML`, `output`) is handed to the build through the harness, and every
distinct config gets its own output folder.

## Differences

- **HTTP against the host, not `dist` or `app.render()`.** Upstream reads the files of `dist` or renders a request with the app of a test adapter. Here the fixture is built with the Jint
  adapter and the page is requested from the host: the file that the build prerendered, or a render in Jint for an on-demand page. The stylesheets, scripts, images and fonts that a page
  links are requested from the host as well (status 200, the content type of the file and, in several tests, the `immutable` cache header of the assets folder). A test that lists a folder
  of the output reads the layout of the adapter: `dist/client` (the files that are served), `dist/server/entry.mjs` with `dist/server/chunks/*.mjs` (the server build is code-split) and
  `astro-jint.json`.
- **One adapter, always.** The harness builds every fixture with `astro-adapter-jint`; it cannot build without an adapter or with a fake one. What upstream builds without an adapter
  (`output: 'static'`) therefore has the layout of the adapter (`client/` and `server/`), and what it renders with a test adapter is a request to the host. The adapter cannot declare
  `buildOutput: 'static'`, so the "server" describes of `build-assets.test.ts` (seven tests) are adapted to the on-demand twin of the page (`/ssr/`) in the same build.
- **Static and server variants.** An upstream test that runs once with `output: 'static'` and once with `output: 'server'` is a `[Theory]` over the two variants: two builds of one fixture
  (`css-inline-stylesheets`, `css-order`, `astro-client-only`, `minification-html`) or one build that has a prerendered page and its on-demand twin (`astro-assets-prefix`,
  `build-assets`). The on-demand pages are rendered by Jint, so a test of the CSS, the scripts or the HTML of a page checks both the build and the engine.
- **`assetQueryParams` is not an adapter option.** `asset-query-params` and `css-pure-chunk-query-params` use a test adapter that declares `client.assetQueryParams` (a deployment id that
  Astro appends to the asset URLs). `astro-adapter-jint` has no such option, so the fixtures wrap its integration (`tests/conformance/support/cb/asset-query-params.mjs`, switched on by the
  `cb.assetQueryParams` flag of the variant), which adds the option when Astro registers the adapter. The host maps a file by its path and ignores the query string. The two inter-chunk
  describes of `asset-query-params.test.ts` are identical (one of them sets `outDir`), so one test covers both. Every import of a client script is requested from the host the way a browser
  resolves it (against the importing script, query string included).
- **Fonts.** `fontProviders.local()` with two font files of the fixture (weights 400 and 500) replaces Fontsource and Google, so that the build needs no network; there are two files because
  Astro preloads a font file once, whatever number of faces use it. The ssr "Exposes buffer from experimental_getFontFileURL()" test fetches the font file from the host that runs it
  (the test adapter of upstream has no limit on `fetch`); the engine rejects every JavaScript `fetch` unless `AstroOptions.FetchUrlFilter` allows it, so that group runs with a host that
  allows loopback addresses, and the test is adapted.
- **CSS modules.** `astro-client-only` has a `.module.css` file where upstream has `.module.scss`: `sass` is an optional peer dependency of Astro, and installing it for one fixture changes
  the resolution of `astro` for every fixture of the lockfile. What the test looks at (the class `_red_` in one stylesheet) does not depend on the preprocessor.
- **Images.** The image service of `image-deletion` and `astro-assets-prefix` is a stub that keeps the bytes of a picture and only sets the format (the `testImageService` of upstream is a
  helper of the Astro repository, not a package). The host does not transcode at `/_image`: `f=webp` for a JPEG answers the stored JPEG, so the SSR test of `image-deletion` that renders
  an on-demand image URL checks that the host answers it with an image.
- **The prerenderer that wraps the default one** (`image-deletion`, three tests, adapted). Upstream builds without an adapter. With an adapter that resolves its own entrypoint, Astro leaves
  the prerender entrypoint out of the build as soon as `setPrerenderer` is called, so the integration of the fixture puts it back in `astro:build:setup`. Astro 7.3.8 hands the wrapper the
  paths themselves from `getStaticPaths()` where the Astro of upstream hands `{ paths, metadata }`, so the wrapper passes on both shapes.
- **Server entry.** In "self entrypoint", rendering a request with the built app of the test adapter is a request to the host, which loads the entry that the manifest of the build
  (`astro-jint.json`) names; the test also checks that name. "serverEntrypoint with entryType: self" keeps its check that `server/custom.mjs` exists, and also checks that the default
  `server/entry.mjs` is not emitted next to it.
- **`featuresSupport`.** Both upstream tests expect an error about `build.middleware` and `build.split`, two features that were removed from Astro, and their assertions sit in a `catch`
  block, so they pass when the build does not throw (it does not; Astro only logs). The first test keeps its title and checks the message that Astro logs for the one feature that the
  adapter declares unsupported, `i18nDomains`, and that the build goes on. Astro 7 checks `i18nDomains` only when `i18n.domains` is set, the output is `server` and `site` is not set, and
  it requires `site` as soon as a domain is listed, so an empty `domains` record is the one config that makes the check log.
- **Underscore in the folder name.** The fixture folder is `_cb-underscore`, and the fake library that the test injects a route from sits next to the config (`node_modules` is ignored by
  git here); an injected route outside `src/pages` in a project path that starts with an underscore is what the test is about. The manifest of the test adapter that lists `/index.html`
  and `/404.html` becomes the listing of `dist/client`, and the pages are requested from the host too.
- **Incremental builds** (`incremental-build-headers`, `incremental-build-islands`, with `experimental.incrementalBuild`). A test builds its fixture twice into one cache (`node_modules/.astro`
  of the fixture). The harness gives every config its own output folder and the folder is not part of what the cache is keyed by, so the second build (a config that differs in the `cb.build`
  flag) writes into another folder and Astro restores the page from its cached copy, as it does after the output folder was emptied; the build log of the second build says `(restored)`.
  Each class deletes the earlier output and the cache first, so that both builds happen in every run. The Content Security Policy that upstream reads from a callback of its test adapter is
  written by this adapter into `astro-jint.json`, from which the host sends it with the file. Astro 7.3.8 names a cached copy by its path from the output folder (`client/plain/a/index.html`)
  where upstream looks for `plain/a/index.html`.
- **Read-only file.** The test makes `public/test.txt` read-only itself (git does not keep the attribute) and deletes the output of earlier builds, so that both builds (static and
  `output: 'server'`) happen with the attribute set; they are builds with the adapter, which lay out `client/` and `server/`.
- **Stricter than upstream.** The loops over elements (`each`) of `astro-assets-prefix` pass upstream when a selector matches nothing; here the stylesheets and the images have to exist. The
  two directory tests of `build-assets` check that the files that the page links are in the listing, and directly in it, where upstream checks that the listing is not empty and that no
  name has a slash (which a `readdir` that is not recursive cannot produce). The first test of `css-server-output-dedup` checks what its comment says, that one file holds the CSS of the
  shared layout and another one the CSS of `/special`. "Build SSR" of `minification-html` checks the whole body for line breaks, where upstream skips its first 20 characters. The tests of
  `container` check status 200. The parity test of `css-order` builds with `inlineStylesheets: 'never'`: upstream compares the linked stylesheets and was written when that was the default,
  and with the `auto` default of Astro 7 these small pages link none.
- **Astro version.** The test sources are those of commit 71e2b24b; the fixtures run the published Astro 7.3.8. Where the two differ (the paths that a wrapping prerenderer gets, the name of
  a cached copy in the incremental build, the teardown of the prerenderer and the resolved Vite output directories), the difference is stated in the test or under "Not applicable".

## Gaps

None open. Every portable upstream test passes.

**GAP-BUILD-1** was found while porting and is fixed on main by `dad60e1` ("Split the server build so content collection entries render"): the adapter built the server as one chunk, so the
CSS of all on-demand pages became one merged stylesheet that every on-demand page linked. Nothing was inlined (`inlineStylesheets: 'auto'` in a server build), the CSS of a shared layout was
emitted twice and `/special` linked no stylesheet of its own. Five tests were skipped with that id and run without a skip since the rebase onto `dad60e1`: `CssInlineStylesheetsTests`
("Setting inlineStylesheets to auto in server output: ..."), `CssOrderTests` ("SSG and SSR parity: ...") and the three tests of `CssServerOutputDedupTests`.

## Not applicable

- **The Astro dev server:** `dev-css-page-boundary.test.ts` (2), the eight tests of the two "dev" groups of `fonts.test.ts` (seven in "shared", one in "Respects config to build links") and
  "in DEV environment" of `minification-html.test.ts` start `astro dev` and request pages from Vite; the host only runs built output.
- **`resolved-vite-output-directories.test.ts` (1):** it needs an Astro core that is newer than the published 7.3.8. A Vite plugin moves the `build.outDir` of the client, ssr and prerender
  environments, and the test asserts that Astro hands the resolved directories to the hooks of a test adapter (`dir` of `astro:build:generated` and `astro:build:done`, `setManifest`, the
  `outputDirectories` of the prerenderer) and that the files are in them; nothing of that is visible over HTTP. A probe with this adapter and Astro 7.3.8 (a fixture with such a plugin, not
  kept) fails inside core before the adapter is involved, with `Cannot find module .../server/.prerender/chunks/server_*.mjs`: the default prerenderer derives the `.prerender` folder from
  the configured `build.server`, not from the `outDir` of the environment. The adapter writes `astro-jint.json` into the configured `outDir` and records `client` and `server` relative to
  it, so it would not follow moved directories either. To port it once Astro supports them: a fixture with the remapping plugin, and the files requested through the host.
- **Other adapter shapes** (`server-entry.test.ts`, 4): "legacy entrypoint" (the adapter lists the `exports` of its `serverEntrypoint` and Astro generates the entry around it, where this adapter
  sets `entrypointResolution: 'auto'` and its entry is the file itself) and the three tests whose fake adapter sets no `serverEntrypoint` and gives Rolldown its own `input` in
  `astro:config:setup`, as a package export string, object and array ("package export (string)", "(object)" and "(array) as rollup self entrypoint"). The adapter under test always sets
  `entrypointResolution: 'auto'` and a `serverEntrypoint`, so Astro never takes those paths with it, and the three tests only check that `server/custom.mjs` exists. The harness cannot host a
  fake adapter either.
- **A feature that no longer exists** (`featuresSupport.test.ts`, 1): "should error if the adapter doesn't support split build"; `build.split` is gone, and upstream asserts only inside `catch`.
- **An order of events inside the build** (`image-deletion.test.ts`, 1): "generates images before teardown"; Astro 7.3.8 tears the prerenderer down before it generates the optimized images
  (the Astro of upstream does it later), so the assertion cannot hold with any adapter, and the output of the build does not show the order.
