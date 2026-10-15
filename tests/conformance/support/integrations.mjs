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

  // What a build for a host without WebAssembly could do for the Code component: Astro asks for the engine of Shiki through
  // the import `#shiki-engine`, which is the Oniguruma engine (WebAssembly) in both of its builds, and this answers it with
  // the engine of Shiki written in JavaScript.
  'cf-shiki-js-engine': () => {
    const moduleId = '\0conformance-cf-shiki-js-engine';
    let engine;
    return {
      name: 'conformance-cf-shiki-js-engine',
      hooks: {
        'astro:config:setup': ({ updateConfig }) => {
          updateConfig({
            vite: {
              plugins: [
                {
                  name: 'conformance-cf-shiki-js-engine',
                  enforce: 'pre',
                  async resolveId(id, importer, options) {
                    if (id !== '#shiki-engine') {
                      return undefined;
                    }

                    engine = (await this.resolve('shiki/engine/javascript', importer, { ...options, skipSelf: true }))?.id;
                    return engine ? moduleId : undefined;
                  },
                  load(id) {
                    return id === moduleId
                      ? `import { createJavaScriptRegexEngine } from ${JSON.stringify(engine)};
export function loadShikiEngine() { return createJavaScriptRegexEngine(); }`
                      : undefined;
                  },
                },
              ],
            },
          });
        },
      },
    };
  },
};

/** @param {{ conformanceIntegration: string } & Record<string, unknown>} descriptor */
export function createIntegration({ conformanceIntegration, ...options }) {
  const factory = factories[conformanceIntegration];
  if (!factory) {
    throw new Error(`There is no conformance integration named '${conformanceIntegration}'.`);
  }

  return factory(options);
}
