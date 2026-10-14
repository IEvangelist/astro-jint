import { mkdirSync, writeFileSync } from 'node:fs';
import { dirname } from 'node:path';
import { fileURLToPath } from 'node:url';
import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

export default defineConfig(withVariant({
  output: 'server',
  adapter: jint(),
  // The upstream suite was authored when inlineStylesheets defaulted to never.
  build: { inlineStylesheets: 'never' },
  integrations: [
    {
      name: 'inject-routes',
      hooks: {
        'astro:config:setup': ({ config, injectRoute }) => {
          injectRoute({
            pattern: '/path-alias/[id]',
            entrypoint: './src/pages/api/products/[id].js',
          });

          // The entrypoint has `.astro` twice in its path: as the folder and as the extension.
          const entrypoint = fileURLToPath(new URL('.astro/test.astro', config.root));
          mkdirSync(dirname(entrypoint), { recursive: true });
          writeFileSync(entrypoint, '<h1>Index</h1>');

          injectRoute({
            pattern: '/test',
            entrypoint,
          });
        },
      },
    },
  ],
}));
