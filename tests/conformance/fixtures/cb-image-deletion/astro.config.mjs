import { existsSync, readdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import markdoc from '@astrojs/markdoc';
import mdx from '@astrojs/mdx';
import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { cbFlags, withCbVariant } from '../../support/cb/variant.mjs';

const { wrappingPrerenderer } = cbFlags();

/** Resolves an image while the prerender bundle loads, outside of any render. */
const outsideRender = {
  name: 'outside-render',
  hooks: {
    'astro:config:setup': ({ addRenderer }) => {
      addRenderer({
        name: 'outside-render',
        serverEntrypoint: new URL('./src/outside-render.ts', import.meta.url),
      });
    },
  },
};

// The integration of the "build ssg with a prerenderer wrapping the default one" tests of upstream, turned on by the
// `cb: { wrappingPrerenderer: true }` flag: it wraps the default prerenderer and drops `/outside-render` from its paths.
// A test cannot see a variable of the build, so the images that exist when the wrapper is torn down go to the build log.
let assetsDir;
const wrapping = {
  name: 'wrapping-prerenderer',
  hooks: {
    'astro:config:done': ({ config }) => {
      assetsDir = new URL(`${config.build.assets}/`, config.build.client);
    },
    'astro:build:setup': ({ vite }) => {
      // With an adapter that resolves its own entrypoint, Astro leaves the prerender entrypoint out as soon as a prerenderer is
      // set, because it expects the prerenderer to bring one. Upstream builds without an adapter, so the default one stays.
      vite.environments.prerender.build.rolldownOptions.input = 'astro/entrypoints/prerender';
    },
    'astro:build:start': ({ setPrerenderer, logger }) => {
      setPrerenderer((defaultPrerenderer) => ({
        name: 'wrapping-prerenderer',
        setup: () => defaultPrerenderer.setup?.(),
        async getStaticPaths() {
          const result = await defaultPrerenderer.getStaticPaths();
          // Wrappers work on the paths, as long as they pass `metadata` through: Astro 7.3.8 returns the paths themselves,
          // the Astro of upstream returns `{ paths, metadata }`.
          const keep = (paths) => paths.filter((path) => path.pathname.replace(/\/$/, '') !== '/outside-render');
          return Array.isArray(result) ? keep(result) : { ...result, paths: keep(result.paths) };
        },
        render: (request, options) => defaultPrerenderer.render(request, options),
        async teardown() {
          const images = existsSync(assetsDir) ? readdirSync(assetsDir).filter((name) => /^onlyone\..*\.webp$/.test(name)) : [];
          logger.info(`onlyone images at teardown: ${images.length}`);
          await defaultPrerenderer.teardown?.();
        },
      }));
    },
  },
};

export default defineConfig(withCbVariant({
  adapter: jint(),
  integrations: [mdx(), markdoc(), outsideRender, ...(wrappingPrerenderer ? [wrapping] : [])],
  image: {
    service: { entrypoint: fileURLToPath(new URL('./src/image-service.mjs', import.meta.url)), config: {} },
  },
}));
