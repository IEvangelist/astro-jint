import { fileURLToPath } from 'node:url';
import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

export default defineConfig(withVariant({
  output: 'server',
  adapter: jint(),
  integrations: [
    {
      name: 'my-middleware',
      hooks: {
        'astro:config:setup': ({ addMiddleware }) => {
          addMiddleware({
            entrypoint: fileURLToPath(new URL('./integration-middleware-pre.js', import.meta.url)),
            order: 'pre',
          });

          addMiddleware({
            entrypoint: fileURLToPath(new URL('./integration-middleware-post.js', import.meta.url)),
            order: 'post',
          });
        },
      },
    },
  ],
}));
