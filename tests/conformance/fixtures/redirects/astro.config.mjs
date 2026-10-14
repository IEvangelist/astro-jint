import { defineConfig } from 'astro/config';
import jint from 'astro-adapter-jint';
import { withVariant } from '../../support/variant.mjs';

export default defineConfig(withVariant({
	adapter: jint(),
	redirects: {
		'/old-page': '/new-page',
		'/old-page-301': {
			status: 301,
			destination: '/new-page-301',
		},
		'/old-page-302': {
			status: 302,
			destination: '/new-page-302',
		},
		'/dynamic/[slug]': '/pages/[slug]',
		'/spread/[...path]': '/content/[...path]',
		'/external': 'https://example.com',
	},
}));
