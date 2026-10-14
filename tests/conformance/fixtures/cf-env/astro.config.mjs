import { defineConfig, envField } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The upstream fixture astro-env. Its wrangler vars (API_URL, PORT) become the build-time variables of
// .env.production, and its .dev.vars secret (API_SECRET) is set through AstroOptions.Env by the tests.
export default defineConfig(
  withVariant({
    env: {
      schema: {
        API_URL: envField.string({ context: 'client', access: 'public', optional: true }),
        PORT: envField.number({ context: 'server', access: 'public', default: 4321 }),
        API_SECRET: envField.string({ context: 'server', access: 'secret' }),
      },
    },
    adapter: jint(),
    output: 'server',
  }),
);
