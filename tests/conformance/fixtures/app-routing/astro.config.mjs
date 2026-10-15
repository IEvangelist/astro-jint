import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The routes of the routing App.render() unit tests under packages/astro/test/units/routing (routing-app, rewrite-app,
// rewrite-validation, get-params and the like). Each upstream test group builds its own route table, so a variant of this
// fixture carries both: the usual config keys (base, trailingSlash, i18n, redirects) and two lists of its own.
//
// `routes` is a list of { pattern, entrypoint, prerender, data } that become injected routes pointing at the components of
// src/routes. `data` is for the components that take their content from the variant, as upstream's inline components do
// (html.astro, rewrite.astro, path-page.astro): they read it from __ROUTE_DATA__ by the pattern of the route they render.
//
// `middleware` is a list of names of the files of src/middleware-library, added as middleware in that order.
//
// There is no src/pages folder and no src/middleware.ts.
const { routes = [], middleware = [], ...config } = withVariant({
  output: 'server',
  adapter: jint(),
});

const routeData = Object.fromEntries(routes.filter((route) => 'data' in route).map((route) => [route.pattern, route.data]));

export default defineConfig({
  ...config,
  vite: { define: { __ROUTE_DATA__: JSON.stringify(routeData) } },
  integrations: [
    {
      name: 'app-routing-routes',
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
