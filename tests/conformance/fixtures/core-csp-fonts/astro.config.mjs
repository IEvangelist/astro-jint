import { defineConfig, fontProviders } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

export default defineConfig(withVariant({
  adapter: jint(),
  security: {
    csp: {
      directives: ["font-src 'self' https://fonts.cdn.test.com"],
    },
  },
  fonts: [
    {
      name: 'Roboto',
      cssVariable: '--font-roboto',
      provider: fontProviders.local(),
      options: {
        variants: [
          {
            weight: 400,
            style: 'normal',
            src: ['./src/fonts/roboto-normal-400.woff2'],
          },
        ],
      },
      optimizedFallbacks: false,
    },
  ],
}));
