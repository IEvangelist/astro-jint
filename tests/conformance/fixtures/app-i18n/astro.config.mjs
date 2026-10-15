import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The routes of the i18n App.render() unit tests under packages/astro/test/units/i18n (i18n-app and the others that
// follow it). Each upstream test group builds its own route table with an i18n config, so a variant of this fixture carries
// both: the usual config keys (i18n, site, base, trailingSlash, security) and `routes`, a list of { pattern, entrypoint,
// prerender } that become injected routes pointing at the components of src/routes. Routes that cannot live in one
// src/pages folder, like /[locale] beside /en/[...slug], are in different variants.
const { routes = [], ...config } = withVariant({
  output: 'server',
  adapter: jint(),
});

export default defineConfig({
  ...config,
  integrations: [
    {
      name: 'app-i18n-routes',
      hooks: {
        'astro:config:setup': ({ injectRoute }) => {
          for (const route of routes) {
            injectRoute(route);
          }
        },
      },
    },
  ],
});
