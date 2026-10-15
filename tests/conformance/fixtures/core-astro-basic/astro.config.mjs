import { writeFile } from 'node:fs/promises';
import mdx from '@astrojs/mdx';
import preact from '@astrojs/preact';
import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// A copy of upstream's `test-prerenderer.ts`: it wraps the default prerenderer, delegating to it, and counts the calls. The calls happen in
// the build process, which a test cannot see, so the counts are written to `prerenderer-calls.json` in the build output.
function recordingPrerenderer() {
  const calls = { setup: 0, getStaticPaths: 0, render: 0, teardown: 0 };
  const renderedPaths = [];
  return {
    name: 'test-prerenderer-integration',
    hooks: {
      'astro:build:start': ({ setPrerenderer }) => {
        setPrerenderer((defaultPrerenderer) => ({
          name: 'test-prerenderer',
          async setup() {
            calls.setup++;
            if (defaultPrerenderer?.setup) {
              await defaultPrerenderer.setup();
            }
          },
          async getStaticPaths() {
            calls.getStaticPaths++;
            return defaultPrerenderer.getStaticPaths();
          },
          async render(request, { routeData }) {
            calls.render++;
            renderedPaths.push(new URL(request.url).pathname);
            return defaultPrerenderer.render(request, { routeData });
          },
          async teardown() {
            calls.teardown++;
            if (defaultPrerenderer?.teardown) {
              await defaultPrerenderer.teardown();
            }
          },
        }));
      },
      'astro:build:done': async ({ dir }) => {
        await writeFile(new URL('prerenderer-calls.json', dir), JSON.stringify({ calls, renderedPaths }));
      },
    },
  };
}

// The upstream project has no adapter and `server: () => ({ port: 4321 })`, a dev server option that a build does not read (it is left
// out). `conformance.customPrerenderer` adds the recording prerenderer, which this adapter cannot be built with (GAP-CORE-4: Astro gives the
// `prerender` environment no input), and `conformance.prerenderEntry` gives that environment its input, `astro/entrypoints/prerender`.
const { conformance, ...config } = withVariant({
  output: 'static',
  adapter: jint(),
  integrations: [preact(), mdx()],
  vite: {
    build: {
      sourcemap: true,
    },
  },
  conformance: {},
});

if (conformance.customPrerenderer) {
  config.integrations = [...config.integrations, recordingPrerenderer()];
}

if (conformance.prerenderEntry) {
  config.vite = {
    ...config.vite,
    plugins: [
      ...(config.vite?.plugins ?? []),
      {
        name: 'conformance:prerender-entry',
        configEnvironment(name) {
          if (name === 'prerender') {
            return { build: { rolldownOptions: { input: 'astro/entrypoints/prerender' } } };
          }
        },
      },
    ],
  };
}

export default defineConfig(config);
