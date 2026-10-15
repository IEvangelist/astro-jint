import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The upstream server-entry fixture (one page) built by the adapter under test instead of the fake adapters.
export default defineConfig(withVariant({
  output: 'server',
  adapter: jint(),
}));
