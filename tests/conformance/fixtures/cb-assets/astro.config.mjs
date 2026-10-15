import { fileURLToPath } from 'node:url';
import mdx from '@astrojs/mdx';
import react from '@astrojs/react';
import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withAssetQueryParams } from '../../support/cb/asset-query-params.mjs';
import { cbFlags, withCbVariant } from '../../support/cb/variant.mjs';

const { assetQueryParams } = cbFlags();

// The astro-assets-prefix fixture of upstream, without its `base` and `build.assetsPrefix` (the tests pass them as config
// changes), plus the pages of the build-assets, astro-assets and asset-query-params-chunks fixtures. Every page exists as a
// prerendered one (src/pages/*) and as an on-demand one (src/pages/ssr/*), where upstream builds the fixture once with
// `output: 'static'` and once with `output: 'server'`. Markdown pages cannot export `prerender`, so an integration does it.
export default defineConfig(withCbVariant({
  output: 'server',
  adapter: assetQueryParams ? withAssetQueryParams(jint(), assetQueryParams) : jint(),
  integrations: [
    react(),
    mdx(),
    { conformanceIntegration: 'prerender-route', componentEndsWith: 'pages/markdown.md' },
  ],
  build: {
    inlineStylesheets: 'never',
  },
  image: {
    service: { entrypoint: fileURLToPath(new URL('./src/image-service.mjs', import.meta.url)), config: {} },
  },
}));
