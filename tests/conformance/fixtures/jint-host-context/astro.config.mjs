import { defineConfig, envField } from 'astro/config';
import jint from 'astro-adapter-jint';

export default defineConfig({
  adapter: jint(),
  env: {
    schema: {
      GREETING: envField.string({ context: 'server', access: 'public', default: 'hello' }),
      API_TOKEN: envField.string({ context: 'server', access: 'secret', optional: true }),
    },
  },
});
