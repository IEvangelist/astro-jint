import react from '@astrojs/react';
import vue from '@astrojs/vue';
import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withAssetQueryParams } from '../../support/cb/asset-query-params.mjs';
import { cbFlags, withCbVariant } from '../../support/cb/variant.mjs';

const { assetQueryParams } = cbFlags();

// The container-custom-renderers fixture of upstream (endpoints that render components with astro/container) and the
// css-pure-chunk-query-params one (a Vue island with two async components that share a stylesheet, page pure-css-chunk), both
// built with `output: 'server'`. The second one needs the deployment id query that the adapter of upstream declares.
export default defineConfig(withCbVariant({
  output: 'server',
  adapter: assetQueryParams ? withAssetQueryParams(jint(), assetQueryParams) : jint(),
  integrations: [react(), vue()],
}));
