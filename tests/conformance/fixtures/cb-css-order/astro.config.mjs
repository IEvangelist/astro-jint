import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The css-order fixture of upstream, plus the transparent page of css-order-layout (the page that has two <style> blocks).
// Upstream builds the pages once as a static site and once with output 'server'; the tests pass `output` as a config change.
export default defineConfig(withVariant({
  output: 'static',
  adapter: jint(),
  integrations: [
    {
      name: 'test-integration',
      hooks: {
        'astro:config:setup'({ injectScript }) {
          injectScript('page-ssr', `import '/src/styles/base.css';`);
        },
      },
    },
  ],
}));
