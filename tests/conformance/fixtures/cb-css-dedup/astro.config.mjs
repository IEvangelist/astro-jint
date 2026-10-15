import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The upstream css-server-output-dedup fixture: two prerendered pages and two on-demand pages that share one layout.
export default defineConfig(withVariant({
  output: 'server',
  adapter: jint(),
  build: { inlineStylesheets: 'never' },
}));
