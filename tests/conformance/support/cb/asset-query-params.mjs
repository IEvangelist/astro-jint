// The adapters of the upstream tests declare `client.assetQueryParams`, the query string (a deployment id, for example)
// that Astro appends to the URLs of the assets it writes into pages. astro-adapter-jint has no option for it, so this
// wraps the integration and adds it when the adapter is registered.

/**
 * @param {import('astro').AstroIntegration} integration the integration of the adapter
 * @param {Record<string, string>} params the query parameters of the asset URLs
 * @returns {import('astro').AstroIntegration}
 */
export function withAssetQueryParams(integration, params) {
  const configDone = integration.hooks['astro:config:done'];
  return {
    ...integration,
    hooks: {
      ...integration.hooks,
      'astro:config:done': (options) =>
        configDone({
          ...options,
          setAdapter: (adapter) =>
            options.setAdapter({ ...adapter, client: { assetQueryParams: new URLSearchParams(params) } }),
        }),
    },
  };
}
