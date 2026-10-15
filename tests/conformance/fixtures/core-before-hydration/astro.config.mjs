import preact from '@astrojs/preact';
import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The upstream tests give some of the builds the `@test/before-hydration` integration inline; the variant
// `{ conformance: { beforeHydration: true } }` adds the same integration here.
const { conformance, ...config } = withVariant({
  adapter: jint(),
  integrations: [preact()],
  conformance: {},
});

if (conformance.beforeHydration) {
  config.integrations.push({
    name: '@test/before-hydration',
    hooks: {
      'astro:config:setup'({ injectScript }) {
        injectScript('before-hydration', `import '/src/scripts/global.js';`);
      },
    },
  });
}

export default defineConfig(config);
