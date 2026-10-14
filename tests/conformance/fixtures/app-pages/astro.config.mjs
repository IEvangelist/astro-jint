import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The routes of the App.render() unit tests under packages/astro/test/units/app, which build theirs inline, as real
// pages. A test picks the variant it needs through the config overrides (trailingSlash, base, security); `site` is what
// ssr-api-route sets, and `inlineStylesheets: 'never'` gives the 404 page a stylesheet link that a test can find.
export default defineConfig(
  withVariant({
    output: 'server',
    adapter: jint(),
    site: 'https://mysite.dev/subsite/',
    build: { inlineStylesheets: 'never' },
  }),
);
