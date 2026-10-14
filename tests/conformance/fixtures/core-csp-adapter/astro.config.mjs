import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

export default defineConfig(withVariant({
  adapter: jint({ staticHeaders: true }),
  security: {
    csp: true,
  },
}));
