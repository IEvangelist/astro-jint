# Spike S1: shape of the Astro build output

**Question.** What does `astro build` produce for a hybrid site, and which build
settings give Jint the simplest possible server bundle?

**Setup.** Astro 7.3.6 (newest stable on the corporate npm mirror), `output: 'static'`,
a throwaway adapter (`spikes/astro-app/astro.config.mjs`), one prerendered page
(`/`) and one on-demand page (`/time`, `export const prerender = false`).

## Layout

```
dist/
  client/index.html        prerendered pages, public/ files, _astro/* assets
  server/entry.mjs         on-demand runtime: default export { fetch }
  server/chunks/*.mjs      lazy chunks (pages, middleware, session driver, ...)
```

Prerendered routes exist only under `dist/client`. On-demand routes exist only in
the server bundle. The adapter's `serverEntrypoint` becomes `entry.mjs`.

## Variants

| Variant | Settings (on top of the adapter) | Result |
| --- | --- | --- |
| base | none | 14 files, 0.9 MB. `[config] adapter does not support "sharp"` error. |
| e1 | `image.service = passthroughImageService()`, `vite.define['process.env.NODE_ENV'] = '"production"'` | 13 files, 0.6 MB. Zero `node:*` imports. Relative dynamic `import()` for pages, middleware, session driver. |
| e2 | e1 + `output.codeSplitting: false` | Build fails: multiple inputs are not supported. Dead end. |
| **e3** | e1 + `vite.ssr = { noExternal: true, target: 'webworker' }` | **One file**, `server/entry.mjs`, 600 KB. Zero `node:*` imports, zero dynamic imports, no `import.meta` use. |

The single file is not guaranteed: server islands (and any other dynamic `import()` in
on-demand code) are emitted as extra chunks, for example `server/chunks/Island_*.mjs`
loaded from `entry.mjs` with `() => import("./chunks/Island_*.mjs")`. The host handles a
multi-file server folder (see S2).

> **Update.** The adapter no longer builds e3 as is: it also sets
> `environments.ssr.build.rolldownOptions.output.codeSplitting: true`, so the server build
> is `entry.mjs` plus `chunks/*.mjs` (the layout of e1) with the dependencies bundled and
> no `node:*` imports (the rest of e3). With the `webworker` target the dynamic imports
> are folded into a single chunk, and Astro 7.3.8 then patches that chunk twice (the manifest
> and the assets of content collection entries, in `manifestBuildPostHook` and
> `contentAssetsBuildPostHook`), the second pass starting from the original code. The
> manifest placeholder stayed in `entry.mjs` of every site whose on-demand page renders an
> MDX entry (conformance gap GAP-CORE-1), and every request answered 500.

## What the default (chunked) bundle needs at runtime

- `sharp` (326 KB chunk) pulls in `node:child_process`, `node:crypto`, `node:os`,
  `node:stream`, `node:util`, `process.platform/arch/versions`, `Buffer`,
  `WebAssembly`. It is only loaded lazily when an image service call happens, but
  it makes the bundle impossible to run under Jint. Passthrough image service
  removes it.
- Rolldown's runtime shim `var __require = createRequire(import.meta.url)` imports
  `node:module` and runs when the shared chunk loads. `webworker` target removes it.
- `process.env.NODE_ENV` is read **unguarded** (3 sites in the server runtime).
  `vite.define` replaces it at build time.
- Guarded globals: `globalThis.process || {}` (piccolore, colors) and
  `typeof process === 'object' && process.versions?.node` / `typeof Buffer`
  (devalue base64 path selection). **A `process` shim must not declare
  `versions.node`**, otherwise devalue picks the `Buffer` path.
- `Uint8Array.fromBase64` is used when present.

## Recipe the adapter applies

In `astro:config:setup`:

1. `image.service = passthroughImageService()` and `sharpImageService: 'unsupported'`
   in `supportedAstroFeatures` (otherwise Astro logs the sharp config error).
2. `vite.define['process.env.NODE_ENV'] = '"production"'`.
3. `vite.ssr = { noExternal: true, target: 'webworker' }`. This also selects
   `worker`/`browser` package export conditions, which is what UI frameworks need
   to avoid Node-only server builds (for example `react-dom/server`).

In `astro:build:done`: write `dist/astro-jint.json` (see the plan: base,
trailingSlash, buildFormat, assetsDir, entry, routes with prerender flag).

## Tooling notes

- The corporate npm mirror lags: `astro@7.3.8` did not resolve, `7.3.6` did.
- pnpm 11 blocks dependency build scripts by default; `esbuild` needs
  `allowBuilds: { esbuild: true }` in `pnpm-workspace.yaml`.
- Build time for the sample is under one second; the output is unminified.

## Open questions handed to S2

- Does Jint run the single-file bundle (parse time, language coverage)?
- Do the chunked variant's relative dynamic imports work under `UseModules`?
- Which Web APIs does the runtime touch on the render path (`Request`, `Response`,
  `Headers`, `URL`, `crypto`, `TextEncoder`, `ReadableStream`, `performance`)?
