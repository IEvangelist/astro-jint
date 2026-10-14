import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

export default defineConfig(withVariant({
  output: 'server',
  adapter: jint(),
  security: {
    allowedDomains: [
      { hostname: 'abc.xyz', port: '444' },
      { hostname: 'legitimate.example.com' },
      { hostname: 'localhost', port: '3000' },
    ],
  },
}));
