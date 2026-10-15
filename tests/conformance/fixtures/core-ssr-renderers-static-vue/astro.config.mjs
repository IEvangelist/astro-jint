import vue from '@astrojs/vue';
import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The variant `{ conformance: { onDemandPage: true } }` makes the page that says `prerender = true` an on-demand page, to show what
// the server entry looks like when a page does need the Vue renderer at request time (not part of the upstream test).
const { conformance, ...config } = withVariant({
  output: 'server',
  adapter: jint(),
  integrations: [vue()],
  conformance: {},
});

export default defineConfig({
  ...config,
  integrations: [
    ...config.integrations,
    {
      name: 'on-demand-page',
      hooks: {
        'astro:route:setup': ({ route }) => {
          if (conformance.onDemandPage && route.component.endsWith('src/pages/index.astro')) {
            route.prerender = false;
          }
        },
      },
    },
  ],
});
