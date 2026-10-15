import { writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import type { AstroConfig, AstroIntegration, IntegrationResolvedRoute } from 'astro';
import { passthroughImageService } from 'astro/config';
import { MANIFEST_FILE_NAME, MANIFEST_VERSION, type AstroJintManifest, type AstroJintStaticHeaders } from './manifest.js';
import { prismForWorkers } from './prism.js';
import { collectStaticHeaders } from './static-headers.js';
import {
  describeUnresolvableImports,
  reportUnresolvableImports,
  type UnresolvableImport,
} from './server-imports.js';

export type { AstroJintAllowedDomain, AstroJintManifest, AstroJintRoute, AstroJintStaticHeaders } from './manifest.js';

const ADAPTER_NAME = 'astro-adapter-jint';
const SHARP_SERVICE_ENTRYPOINT = 'astro/assets/services/sharp';
const IMAGE_ENDPOINT_ENTRYPOINT = `${ADAPTER_NAME}/image-endpoint`;
// Astro resolves a driver relative to its own files, where a bare specifier is not found when the
// package manager does not hoist this adapter. A file URL always is.
const SESSION_DRIVER_ENTRYPOINT = new URL('./session-driver.js', import.meta.url);
// What `sessionDrivers.memory()` returns, which the typings of `sessionDrivers` leave out.
const MEMORY_SESSION_DRIVER_ENTRYPOINT = 'unstorage/drivers/memory';

export interface JintAdapterOptions {
  /**
   * Minify the server bundle. Roughly halves its size, but parse time barely changes and stack
   * traces get harder to read, so it is off by default.
   * @default false
   */
  minify?: boolean;

  /**
   * Send the `Content-Security-Policy` header with prerendered pages instead of a `<meta>` tag, like the
   * `staticHeaders` option of the official adapters. A header can carry directives that a `<meta>` tag
   * cannot (`frame-ancestors`, `report-uri`, `sandbox`). It only applies when `security.csp` is set.
   * @default false
   */
  staticHeaders?: boolean;
}

/**
 * Astro adapter that renders on-demand routes inside Jint, hosted by ASP.NET Core.
 * Prerendered pages stay plain files in `dist/client`; the ASP.NET Core host serves them directly.
 */
export default function jint(options: JintAdapterOptions = {}): AstroIntegration {
  let config: AstroConfig | undefined;
  let routes: IntegrationResolvedRoute[] = [];
  let staticHeaders: AstroJintStaticHeaders[] = [];
  let unresolvableImports: UnresolvableImport[] = [];

  return {
    name: ADAPTER_NAME,
    hooks: {
      'astro:config:setup': ({ config: userConfig, command, logger, updateConfig }) => {
        // Without this a static site's redirects become meta-refresh pages that answer 200.
        // Left off, the runtime answers them with the configured 3xx status instead.
        updateConfig({ build: { redirects: false } });

        // The default Sharp service drags node:child_process, node:os and native bindings into the
        // server bundle. A service the user configured explicitly is left alone.
        if (userConfig.image.service.entrypoint === SHARP_SERVICE_ENTRYPOINT) {
          updateConfig({ image: { service: passthroughImageService() } });
        }

        // Sessions work out of the box, like they do with the official adapters. `session: false`
        // turns them off, which also drops the session runtime from the bundle, and a driver the
        // user configured is left alone.
        const { session } = userConfig;
        if (session !== false && !session?.driver) {
          const onBuild = command === 'build';
          logger.info(
            onBuild
              ? 'Enabling sessions with the distributed cache of the ASP.NET Core host'
              : 'Enabling sessions with in-memory storage',
          );
          updateConfig({
            session: {
              // There is no host to talk to under `astro dev`, so sessions live in memory there.
              driver: { entrypoint: onBuild ? SESSION_DRIVER_ENTRYPOINT : MEMORY_SESSION_DRIVER_ENTRYPOINT },
              cookie: session?.cookie,
              ttl: session?.ttl,
            },
          });
        }

        if (command === 'build') {
          // Astro's own image endpoints need a network route to the app or a file system.
          if (userConfig.image.endpoint.entrypoint === undefined) {
            updateConfig({
              image: { endpoint: { route: userConfig.image.endpoint.route, entrypoint: IMAGE_ENDPOINT_ENTRYPOINT } },
            });
          }

          updateConfig({
            vite: {
              define: { 'process.env.NODE_ENV': '"production"' },
              // Every dependency is bundled and none imports a node:* module, which is what Jint can evaluate.
              ssr: {
                noExternal: true,
                target: 'webworker',
                // The target only adds `browser` to the conditions. Packages that ship a build for workers expose it as
                // `worker` (solid-js does, and its `browser` build cannot render to a string), so it is asked for first.
                resolve: { conditions: ['worker', 'module', 'browser', 'development|production'] },
              },
              plugins: [
                // What is still imported from outside of the bundle is reported when the build is done.
                reportUnresolvableImports((found) => unresolvableImports.push(...found)),
                // The default loader of `@astrojs/prism` needs `require()`, which neither the bundle nor Jint has.
                prismForWorkers(),
              ],
              environments: {
                ssr: {
                  build: {
                    rolldownOptions: {
                      output: {
                        // The `webworker` target folds the dynamic imports of a single entry into one chunk. Astro then
                        // injects the manifest and the assets of content collection entries into that chunk in two
                        // passes, and the second starts from the original code, so the manifest placeholder stays in
                        // the bundle and no request can be answered. Separate chunks are the layout Astro expects.
                        codeSplitting: true,
                        // Astro hard-codes `build.minify: false` for the server build, so the only way to
                        // minify it is through the output options of the ssr environment.
                        ...(options.minify && { minify: true }),
                      },
                    },
                  },
                },
              },
            },
          });
        }
      },

      'astro:config:done': ({ config: resolved, setAdapter }) => {
        config = resolved;
        setAdapter({
          name: ADAPTER_NAME,
          entrypointResolution: 'auto',
          serverEntrypoint: fileURLToPath(new URL('./server.js', import.meta.url)),
          adapterFeatures: { buildOutput: 'server', staticHeaders: options.staticHeaders ?? false },
          supportedAstroFeatures: {
            staticOutput: 'stable',
            hybridOutput: 'stable',
            serverOutput: 'stable',
            envGetSecret: 'stable',
            sharpImageService: 'unsupported',
            i18nDomains: 'unsupported',
          },
        });
      },

      'astro:routes:resolved': ({ routes: resolved }) => {
        routes = resolved;
      },

      'astro:build:start': () => {
        unresolvableImports = [];
      },

      'astro:build:generated': ({ dir, routeToHeaders }) => {
        // Astro only fills the map when the adapter has `staticHeaders` on, and the policy of a page
        // that is not under `security.csp` is not Astro's to replay.
        if (config?.security.csp) {
          staticHeaders = collectStaticHeaders(routeToHeaders, fileURLToPath(dir));
        }
      },

      'astro:build:done': async ({ logger }) => {
        if (!config) {
          throw new Error(`${ADAPTER_NAME}: the Astro config was not resolved before the build finished.`);
        }

        const manifest = createManifest(config, routes, staticHeaders);
        const outDir = fileURLToPath(config.outDir);
        await writeFile(path.join(outDir, MANIFEST_FILE_NAME), `${JSON.stringify(manifest, null, 2)}\n`);

        const prerendered = manifest.routes.filter((r) => r.prerender && r.origin === 'project').length;
        const onDemand = manifest.routes.filter((r) => !r.prerender && r.origin === 'project').length;
        logger.info(`${MANIFEST_FILE_NAME}: ${prerendered} prerendered and ${onDemand} on-demand routes`);

        for (const message of describeUnresolvableImports(unresolvableImports, manifest.server)) {
          logger.warn(message);
        }
      },
    },
  };
}

function createManifest(
  config: AstroConfig,
  routes: IntegrationResolvedRoute[],
  staticHeaders: AstroJintStaticHeaders[],
): AstroJintManifest {
  const outDir = fileURLToPath(config.outDir);
  const relativeToOutDir = (target: URL) => path.relative(outDir, fileURLToPath(target)).split(path.sep).join('/');
  const server = relativeToOutDir(config.build.server);
  const adapterPackage = readPackageJson('../package.json');

  return {
    version: MANIFEST_VERSION,
    adapter: { name: ADAPTER_NAME, version: adapterPackage?.version ?? 'unknown' },
    astroVersion: readPackageJson('astro/package.json')?.version ?? null,
    base: normalizeBase(config.base),
    trailingSlash: config.trailingSlash,
    buildFormat: config.build.format,
    assetsDir: config.build.assets,
    client: relativeToOutDir(config.build.client),
    server,
    entry: `${server}/${config.build.serverEntry}`,
    allowedDomains: config.security.allowedDomains.map(({ protocol, hostname, port }) => ({ protocol, hostname, port })),
    staticHeaders,
    routes: routes.map((route) => ({
      route: route.pattern,
      type: route.type,
      prerender: route.isPrerendered,
      origin: route.origin,
    })),
  };
}

function normalizeBase(base: string): string {
  const withLeadingSlash = base.startsWith('/') ? base : `/${base}`;
  return withLeadingSlash.length > 1 ? withLeadingSlash.replace(/\/+$/, '') : '/';
}

function readPackageJson(specifier: string): { version?: string } | undefined {
  try {
    return createRequire(import.meta.url)(specifier);
  } catch {
    return undefined;
  }
}
