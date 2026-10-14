import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// `session: false` and the default (sessions enabled by the adapter) are both selected from the tests with a JSON override.
export default defineConfig(withVariant({
  output: 'server',
  adapter: jint(),
}));
