import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The upstream test adapter sets `client.assetQueryParams` through `extendAdapter`. The Jint adapter has no such option, so a
// variant asks for it with `conformance.assetQueryParams` and this decorates the adapter's `setAdapter()` call to carry it.
function withAssetQueryParams(adapter, queryParams) {
  const configDone = adapter.hooks['astro:config:done'];
  return {
    ...adapter,
    hooks: {
      ...adapter.hooks,
      'astro:config:done': (options) =>
        configDone({
          ...options,
          setAdapter: (value) =>
            options.setAdapter({ ...value, client: { assetQueryParams: new URLSearchParams(queryParams) } }),
        }),
    },
  };
}

const { conformance, ...config } = withVariant({ output: 'server', adapter: jint(), conformance: {} });

if (conformance.assetQueryParams) {
  config.adapter = withAssetQueryParams(config.adapter, conformance.assetQueryParams);
}

export default defineConfig(config);
