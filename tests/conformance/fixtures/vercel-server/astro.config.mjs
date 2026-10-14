import { defineConfig } from 'astro/config';
import sitemap from '@astrojs/sitemap';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// One site for the on-demand tests of the upstream @astrojs/vercel suite: the fixtures of serverless-with-dynamic-routes,
// prerendered-error-pages, server-rendered-error-pages, server-islands, static-assets, static-headers, integration-assets,
// image, isr and isr-with-edge-middleware, merged to save builds. The variants of the tests change `build.assets` and make
// the 404 page prerendered.
export default defineConfig(withVariant({
  output: 'server',
  site: 'https://example.com',
  adapter: jint({ staticHeaders: true }),
  integrations: [sitemap()],
  redirects: { '/old': '/' },
  security: { csp: true },
  build: {
    // Styles are emitted as files so that the site has a hashed asset to cache.
    inlineStylesheets: 'never',
  },
  vite: {
    build: {
      assetsInlineLimit: 0,
    },
  },
}));
