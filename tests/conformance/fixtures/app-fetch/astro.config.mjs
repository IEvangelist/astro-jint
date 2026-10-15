import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The app of the tests of packages/astro/test/units/fetch/index.test.ts. Upstream builds a test App with inline routes and
// calls the functions of `astro/fetch` (FetchState, trailingSlash, redirects, actions, middleware, pages, i18n, astro) on a
// FetchState it creates itself. Here src/fetch.ts is the fetch handler of the app (the `fetchFile` of Astro) and runs the
// function, or the composition of functions, that the `x-pipeline` header of the request names.
//
// Each variant lists the routes it needs in `routes` ({ pattern, entrypoint, prerender }, the entrypoint being a file of
// src/routes), and the names of the files of src/middleware-library to add as middleware in `middleware`. The other keys are
// the usual config (trailingSlash, i18n, redirects, security). There is no src/pages folder and no src/middleware.ts.
const { routes = [], middleware = [], ...config } = withVariant({
  output: 'server',
  adapter: jint(),
});

export default defineConfig({
  ...config,
  integrations: [
    {
      name: 'app-fetch-routes',
      hooks: {
        'astro:config:setup': ({ injectRoute, addMiddleware }) => {
          for (const { pattern, entrypoint, prerender } of routes) {
            injectRoute({ pattern, entrypoint, prerender });
          }
          for (const name of middleware) {
            addMiddleware({ entrypoint: new URL(`./src/middleware-library/${name}.ts`, import.meta.url), order: 'pre' });
          }
        },
      },
    },
  ],
});
