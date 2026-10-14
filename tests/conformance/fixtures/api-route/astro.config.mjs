import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

export default defineConfig(withVariant({
  output: 'server',
  adapter: jint(),
  security: {
    checkOrigin: false,
    allowedDomains: [{ hostname: 'localhost', port: '4321' }],
  },
}));
