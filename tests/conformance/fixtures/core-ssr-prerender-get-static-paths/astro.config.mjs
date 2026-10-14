import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// With `output: 'static'` the pages are prerendered by default, which the upstream suite reaches by stripping the
// `export const prerender = true;` of the pages with a Vite plugin.
function vitePluginRemovePrerenderExport() {
  const EXTENSIONS = ['.astro', '.ts'];
  const plugin = {
    name: 'remove-prerender-export',
    transform(code, id) {
      if (!EXTENSIONS.some((ext) => id.endsWith(ext))) return;
      return code.replace(/export\s+const\s+prerender\s+=\s+true;/g, '');
    },
  };
  return {
    name: 'remove-prerender-export-injector',
    configResolved(resolved) {
      resolved.plugins.unshift(plugin);
    },
  };
}

export default defineConfig(withVariant({
  site: 'https://mysite.dev/',
  base: '/blog',
  output: 'server',
  adapter: jint(),
  integrations: [
    {
      name: 'remove-prerender-export',
      hooks: {
        'astro:config:setup': ({ config, updateConfig }) => {
          if (config.output === 'static') {
            updateConfig({ vite: { plugins: [vitePluginRemovePrerenderExport()] } });
          }
        },
      },
    },
  ],
}));
