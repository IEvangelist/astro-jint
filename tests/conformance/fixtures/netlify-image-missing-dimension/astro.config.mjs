import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

// The fixture of upstream (test/static/fixtures/image-missing-dimension). The build is expected to fail.
export default defineConfig(withVariant({
	adapter: jint(),
	output: 'static',
	image: {
		service: {
			entrypoint: 'astro/assets/services/sharp',
		},
	},
}));
