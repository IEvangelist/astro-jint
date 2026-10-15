import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// One fixture for the pages of several small upstream fixtures of the Cloudflare adapter tests (build budget): server-entry,
// ssr-deps, ts-astro-import, production-async-hooks, astro-dev-platform (Code and Prism) and the action and the prerendered
// 404 page of prerender-node-env and cf-helpers.
export default defineConfig(
  withVariant({
    output: 'server',
    adapter: jint(),
  }),
);
