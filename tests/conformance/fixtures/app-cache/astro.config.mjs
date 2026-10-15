import { defineConfig, memoryCache } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The routes that the tests of context.cache under packages/astro/test/units/cache/app-cache.test.ts build inline, as real
// endpoints and pages. The upstream tests run them against three manifests, which are three builds of this fixture:
// `{ conformance: { provider: 'memory' } }` (the memory provider of astro/config), `{ conformance: { provider: 'cdn' } }` (a
// provider without onRequest, so Astro only sets CDN-Cache-Control and Cache-Tag; the route rules of the config give /config-route
// its cache options) and none, which is the default build with the cache disabled.
const { conformance, ...config } = withVariant({
  output: 'server',
  adapter: jint(),
  conformance: {},
});

const providers = {
  memory: memoryCache(),
  cdn: { name: 'mock-cdn', entrypoint: './src/cdn-provider.mjs' },
};

export default defineConfig({
  ...config,
  ...(conformance.provider ? { cache: { provider: providers[conformance.provider] } } : {}),
  ...(conformance.provider === 'cdn' ? { routeRules: { '/config-route': { maxAge: 600, tags: ['config'] } } } : {}),
});
