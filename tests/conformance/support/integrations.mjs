// Integrations a test can ask for by name. The upstream tests define small integrations inline, which the JSON overrides of
// a variant cannot carry, so a fixture config resolves entries such as
// { conformanceIntegration: 'prerender-route', componentEndsWith: 'two.astro' } through createIntegration().

const factories = {
  // Marks every route whose component ends with `componentEndsWith` as prerendered, from the `astro:route:setup` hook.
  'prerender-route': ({ componentEndsWith }) => ({
    name: 'conformance-prerender-route',
    hooks: {
      'astro:route:setup': ({ route }) => {
        if (route.component.endsWith(componentEndsWith)) {
          route.prerender = true;
        }
      },
    },
  }),

  // Registers an image service the way an integration does, from `astro:config:setup`, instead of through `image.service`.
  'cf-image-service': ({ entrypoint }) => ({
    name: 'conformance-cf-image-service',
    hooks: {
      'astro:config:setup': ({ updateConfig }) => {
        updateConfig({ image: { service: { entrypoint } } });
      },
    },
  }),
};

/** @param {{ conformanceIntegration: string } & Record<string, unknown>} descriptor */
export function createIntegration({ conformanceIntegration, ...options }) {
  const factory = factories[conformanceIntegration];
  if (!factory) {
    throw new Error(`There is no conformance integration named '${conformanceIntegration}'.`);
  }

  return factory(options);
}
