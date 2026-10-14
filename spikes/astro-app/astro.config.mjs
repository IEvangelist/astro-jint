import { fileURLToPath } from 'node:url';
import { defineConfig, passthroughImageService } from 'astro/config';

// Throwaway adapter for spike S1. Replaced by packages/astro-adapter-jint.
const variant = process.env.SPIKE ?? 'base';
const spikeAdapter = () => ({
  name: 'spike-adapter',
  hooks: {
    'astro:config:setup': ({ updateConfig }) => {
      if (variant === 'base') return;
      const vite = { define: { 'process.env.NODE_ENV': '"production"' } };
      if (variant === 'e2') {
        vite.build = { rollupOptions: { output: { codeSplitting: false } } };
      }
      if (variant === 'e3' || variant === 'e3m') {
        vite.ssr = { noExternal: true, target: 'webworker' };
      }
      if (variant === 'e3m') {
        // Astro hard-codes top-level build.minify=false for the server build, so
        // minification has to be requested through the ssr environment's Rolldown output.
        vite.environments = { ssr: { build: { rolldownOptions: { output: { minify: true } } } } };
      }
      updateConfig({ image: { service: passthroughImageService() }, vite });
    },
    'astro:config:done': ({ setAdapter }) => {
      setAdapter({
        name: 'spike-adapter',
        entrypointResolution: 'auto',
        serverEntrypoint: fileURLToPath(new URL('./spike-server.js', import.meta.url)),
        adapterFeatures: { buildOutput: 'server' },
        supportedAstroFeatures: {
          staticOutput: 'stable',
          hybridOutput: 'stable',
          serverOutput: 'stable',
          sharpImageService: 'unsupported',
          envGetSecret: 'unsupported',
          i18nDomains: 'unsupported',
        },
      });
    },
  },
});

export default defineConfig({
  outDir: variant === 'base' ? './dist' : `./dist-${variant}`,
  adapter: spikeAdapter(),
});
