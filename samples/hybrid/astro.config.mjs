import { defineConfig, envField } from 'astro/config';
import jint from 'astro-adapter-jint';

// Output stays 'static': pages are prerendered unless they set `export const prerender = false`.
export default defineConfig({
  adapter: jint(),
  // Stylesheets and scripts become hashed files in `_astro/`, which the host serves with an immutable cache policy.
  build: { inlineStylesheets: 'never' },
  vite: { build: { assetsInlineLimit: 0 } },
  redirects: { '/news': '/blog' },
  env: {
    schema: {
      GREETING: envField.string({ context: 'server', access: 'public', default: 'Hello from Jint' }),
      API_TOKEN: envField.string({ context: 'server', access: 'secret', optional: true }),
    },
  },
});
