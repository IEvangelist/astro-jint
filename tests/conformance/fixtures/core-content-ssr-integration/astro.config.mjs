import mdx from '@astrojs/mdx';
import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// Same check as upstream's test-plugins.ts `preventNodeBuiltinDependencyPlugin`: `astro:content` must
// not have a hard dependency on Node builtins (Jint provides none).
function preventNodeBuiltinDependencyPlugin() {
  return {
    name: 'verify-no-node-stuff',
    generateBundle() {
      for (const name of ['node:fs', 'node:url', 'node:worker_threads', 'node:path']) {
        if (this.getModuleInfo(name)) {
          throw new Error(`Node builtins snuck in: ${name}`);
        }
      }
    },
  };
}

export default defineConfig(withVariant({
  output: 'server',
  adapter: jint(),
  integrations: [mdx()],
  vite: { plugins: [preventNodeBuiltinDependencyPlugin()] },
}));
