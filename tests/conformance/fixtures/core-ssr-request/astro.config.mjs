import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

export default defineConfig(withVariant({
  output: 'server',
  adapter: jint(),
  base: '/subpath/',
  integrations: [
    {
      name: 'inject-script',
      hooks: {
        'astro:config:setup'({ injectScript }) {
          injectScript('page', 'import "/src/scripts/inject-script.js";');
        },
      },
    },
  ],
  vite: {
    build: {
      // Keeps the CSS and the injected script as files of their own, which the page links to.
      assetsInlineLimit: 0,
    },
  },
}));
