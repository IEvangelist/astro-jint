import { writeFile } from 'node:fs/promises';
import { createRequire } from 'node:module';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import type { AstroConfig, AstroIntegration, IntegrationResolvedRoute } from 'astro';
import { passthroughImageService } from 'astro/config';
import { MANIFEST_FILE_NAME, MANIFEST_VERSION, type AstroJintManifest } from './manifest.js';

export type { AstroJintAllowedDomain, AstroJintManifest, AstroJintRoute } from './manifest.js';

const ADAPTER_NAME = 'astro-adapter-jint';
const SHARP_SERVICE_ENTRYPOINT = 'astro/assets/services/sharp';

export interface JintAdapterOptions {
  /**
   * Minify the server bundle. Roughly halves its size, but parse time barely changes and stack
   * traces get harder to read, so it is off by default.
   * @default false
   */
  minify?: boolean;
}

/**
 * Astro adapter that renders on-demand routes inside Jint, hosted by ASP.NET Core.
 * Prerendered pages stay plain files in `dist/client`; the ASP.NET Core host serves them directly.
 */
export default function jint(options: JintAdapterOptions = {}): AstroIntegration {
  let config: AstroConfig | undefined;
  let routes: IntegrationResolvedRoute[] = [];

  return {
    name: ADAPTER_NAME,
    hooks: {
      'astro:config:setup': ({ config: userConfig, command, updateConfig }) => {
        // Without this a static site's redirects become meta-refresh pages that answer 200.
        // Left off, the runtime answers them with the configured 3xx status instead.
        updateConfig({ build: { redirects: false } });

        // The default Sharp service drags node:child_process, node:os and native bindings into the
        // server bundle. A service the user configured explicitly is left alone.
        if (userConfig.image.service.entrypoint === SHARP_SERVICE_ENTRYPOINT) {
          updateConfig({ image: { service: passthroughImageService() } });
        }

        if (command === 'build') {
          updateConfig({
            vite: {
              define: { 'process.env.NODE_ENV': '"production"' },
              // One self-contained module with no node:* imports, which is what Jint can evaluate.
              ssr: { noExternal: true, target: 'webworker' },
              ...(options.minify && {
                // Astro hard-codes `build.minify: false` for the server build, so the only way to
                // minify it is through the output options of the ssr environment.
                environments: { ssr: { build: { rolldownOptions: { output: { minify: true } } } } },
              }),
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
          adapterFeatures: { buildOutput: 'server' },
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

      'astro:build:done': async ({ logger }) => {
        if (!config) {
          throw new Error(`${ADAPTER_NAME}: the Astro config was not resolved before the build finished.`);
        }

        const manifest = createManifest(config, routes);
        const outDir = fileURLToPath(config.outDir);
        await writeFile(path.join(outDir, MANIFEST_FILE_NAME), `${JSON.stringify(manifest, null, 2)}\n`);

        const prerendered = manifest.routes.filter((r) => r.prerender && r.origin === 'project').length;
        const onDemand = manifest.routes.filter((r) => !r.prerender && r.origin === 'project').length;
        logger.info(`${MANIFEST_FILE_NAME}: ${prerendered} prerendered and ${onDemand} on-demand routes`);
      },
    },
  };
}

function createManifest(config: AstroConfig, routes: IntegrationResolvedRoute[]): AstroJintManifest {
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
