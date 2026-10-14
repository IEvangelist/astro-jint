import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

export default defineConfig(withVariant({
  output: 'server',
  adapter: jint(),
  // we stub actions coming from another domain for testing purposes
  security: {
    checkOrigin: false,
  },
}));
