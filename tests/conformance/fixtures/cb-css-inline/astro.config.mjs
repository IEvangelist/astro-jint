import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The upstream css-inline-stylesheets fixture. Upstream builds it with output 'static' and with output 'server' and changes
// `build.inlineStylesheets` in each test; the tests here pass the same changes as config variants.
export default defineConfig(withVariant({
  output: 'server',
  adapter: jint(),
}));
