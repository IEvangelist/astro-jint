import { defineConfig, envField } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

export default defineConfig(
  withVariant({
    adapter: jint(),
    env: {
      schema: {
        GREETING: envField.string({ context: 'server', access: 'public', default: 'hello' }),
        API_TOKEN: envField.string({ context: 'server', access: 'secret', optional: true }),
      },
    },
  }),
);
