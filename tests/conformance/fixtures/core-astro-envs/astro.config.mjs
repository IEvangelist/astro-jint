import vue from '@astrojs/vue';
import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The upstream tests set variables of the process before they build; the build runs in another process here, so the
// variant `{ conformance: { buildEnv: { NAME: 'value' } } }` sets them from the config.
const { conformance, ...config } = withVariant({
  site: 'http://example.com',
  base: '/blog',
  adapter: jint(),
  integrations: [vue()],
  vite: {
    plugins: [
      {
        // Plugin so that we can see in the tests whether the env has been injected
        name: 'export-env-plugin',
        enforce: 'post',
        transform(code, id) {
          if (id.endsWith('.json')) {
            return `${code}\n export const env = ${JSON.stringify(code.includes('CHESHIRE') || code.includes('process.env.KITTY') ? 'CHESHIRE' : 'A MYSTERY')}`;
          }
        },
      },
    ],
  },
  conformance: { buildEnv: { BOOLEAN_VAR: 'true', NUMBER_VAR: '1' } },
});

Object.assign(process.env, conformance.buildEnv);

export default defineConfig(config);
