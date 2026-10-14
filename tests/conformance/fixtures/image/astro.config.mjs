import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

export default defineConfig(withVariant({
  output: 'server',
  adapter: jint(),
  image: {
    // 127.0.0.1 is not in the upstream fixture: the remote image test serves its picture from a loopback origin.
    domains: ['images.unsplash.com', '127.0.0.1'],
  },
  vite: {
    build: {
      assetsInlineLimit: 0,
    },
  },
}));
