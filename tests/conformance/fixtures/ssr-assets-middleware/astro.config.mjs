import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

export default defineConfig(withVariant({
	output: 'server',
	adapter: jint(),
	vite: {
		build: {
			// Keeps CSS and SVG as separate files in dist/client/_astro.
			assetsInlineLimit: 0,
		},
	},
}));
