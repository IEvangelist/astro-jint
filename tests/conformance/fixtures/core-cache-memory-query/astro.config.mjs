import { defineConfig, memoryCache } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The two upstream fixtures differ in the options of the provider, which a JSON config cannot carry. The variant
// `{ conformance: { queryInclude: ['page', 'sort'] } }` is `memoryCache({ query: { include: [...] } })`; with none the provider
// has its default query config (tracking params such as utm_* and fbclid are excluded, and params are sorted).
const { conformance, ...config } = withVariant({
  output: 'server',
  adapter: jint(),
  conformance: {},
});

export default defineConfig({
  ...config,
  cache: {
    provider: conformance.queryInclude ? memoryCache({ query: { include: conformance.queryInclude } }) : memoryCache(),
  },
});
