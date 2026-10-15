import { fileURLToPath } from 'node:url';
import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The upstream tests give every build the image service they need (`image.service`, `base`, `trailingSlash`, ...) in `loadFixture()`. Here
// the tests pass the same options as a variant. The config has no `image.service` of its own: variants are merged deeply, which would mix the
// `{ entrypoint, config }` shape of the option with its `{ build, runtime }` one.
// `conformance.srcAlias` is the Vite alias `~` (the `src` folder) of `image-service-targets.test.ts`, an absolute path that a JSON variant cannot
// carry, and `conformance.wrappingPrerenderer` is the `wrappingPrerenderer` integration of the same file.
const { conformance, ...config } = withVariant({
  output: 'server',
  adapter: jint(),
  conformance: {},
});

if (conformance.srcAlias) {
  config.vite = {
    ...config.vite,
    resolve: { ...config.vite?.resolve, alias: { '~': fileURLToPath(new URL('./src', import.meta.url)) } },
  };
}

if (conformance.wrappingPrerenderer) {
  config.integrations = [
    ...(config.integrations ?? []),
    {
      name: 'wrapping-prerenderer',
      hooks: {
        'astro:build:start': ({ setPrerenderer }) => {
          setPrerenderer((defaultPrerenderer) => ({ ...defaultPrerenderer, name: 'wrapping' }));
        },
      },
    },
  ];
}

export default defineConfig(config);
