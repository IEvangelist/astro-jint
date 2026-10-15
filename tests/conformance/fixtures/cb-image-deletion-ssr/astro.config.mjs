import { fileURLToPath } from 'node:url';
import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The core-image-deletion-ssr fixture of upstream, with its test adapter replaced by the Jint one.
export default defineConfig(withVariant({
  output: 'server',
  adapter: jint(),
  image: {
    service: { entrypoint: fileURLToPath(new URL('./src/image-service.mjs', import.meta.url)), config: {} },
  },
}));
