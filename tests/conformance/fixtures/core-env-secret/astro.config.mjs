import { defineConfig, envField } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

export default defineConfig(withVariant({
  output: 'server',
  adapter: jint(),
  env: {
    schema: {
      KNOWN_SECRET: envField.number({ context: 'server', access: 'secret' }),
    },
  },
}));
