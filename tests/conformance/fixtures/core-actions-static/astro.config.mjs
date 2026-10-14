import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The upstream test with every page prerendered uses a project that has no on-demand page. The variant
// `{ conformance: { allPrerendered: true } }` reaches that by prerendering the page that says `prerender = false`.
const { conformance, ...config } = withVariant({
  output: 'static',
  adapter: jint(),
  conformance: {},
});

export default defineConfig({
  ...config,
  integrations: [
    {
      name: 'prerender-the-page',
      hooks: {
        'astro:route:setup': ({ route }) => {
          if (conformance.allPrerendered && route.component.endsWith('src/pages/index.astro')) {
            route.prerender = true;
          }
        },
      },
    },
  ],
});
