import { defineConfig, fontProviders } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withAssetQueryParams } from '../../support/cb/asset-query-params.mjs';
import { cbFlags, withCbVariant } from '../../support/cb/variant.mjs';

const { assetQueryParams } = cbFlags();

// The fonts fixture of upstream, which asks Fontsource (or Google) for Poppins and Roboto, with the two font files of this
// folder as a local family, so the build works offline. The tests change `output`, `base` and `build` of this config.
export default defineConfig(withCbVariant({
  adapter: assetQueryParams ? withAssetQueryParams(jint(), assetQueryParams) : jint(),
  fonts: [
    {
      name: 'Test font',
      cssVariable: '--font-test',
      provider: fontProviders.local(),
      weights: [400, 500],
      options: {
        variants: [
          { weight: 400, style: 'normal', src: ['./src/fonts/roboto-normal-400.woff2'] },
          { weight: 500, style: 'normal', src: ['./src/fonts/bitgeneva12.woff2'] },
        ],
      },
    },
  ],
}));
