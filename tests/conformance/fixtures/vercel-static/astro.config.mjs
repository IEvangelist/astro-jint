import { defineConfig } from 'astro/config';
import sitemap from '@astrojs/sitemap';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The static-output counterpart of vercel-server: the fixtures of redirects, redirects-serverless, server-islands-static,
// static, static-assets, integration-assets and image of the upstream @astrojs/vercel suite, merged to save builds. The
// variants of the tests change `trailingSlash` and `build.assets`.
export default defineConfig(withVariant({
  site: 'https://example.com',
  adapter: jint(),
  integrations: [sitemap()],
  redirects: {
    '/one': '/',
    '/two': '/',
    '/three': {
      status: 302,
      destination: '/',
    },
    '/four': {
      status: 302,
      destination: 'http://example.com',
    },
    '/blog/[...slug]': '/team/articles/[...slug]',
    '/Basic/http-2-0.html': '/posts/http2',
    '/other': '/subpage',
    // A redirect from the folder of the hashed files, to see that the cache rule of the folder is after the redirects.
    '/_astro/legacy.css': '/',
  },
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
