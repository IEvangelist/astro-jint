import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

export default defineConfig(withVariant({
  adapter: jint(),
  build: {
    assetsPrefix: 'https://cdn.example.com',
  },
  site: 'https://astro.build/',
  i18n: {
    locales: ['en', 'fr'],
    defaultLocale: 'en',
  },
  integrations: [
    {
      name: 'config',
      hooks: {
        'astro:config:setup': ({ updateConfig }) => {
          updateConfig({
            base: '/blog',
            site: 'https://example.com',
            trailingSlash: 'always',
          });
        },
      },
    },
  ],
}));
