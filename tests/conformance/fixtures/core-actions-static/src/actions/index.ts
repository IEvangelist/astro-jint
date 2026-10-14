import { defineAction } from 'astro:actions';
import { z } from 'astro/zod';

export const server = {
	ping: defineAction({
		handler: async () => {
			return { ok: true };
		},
	}),
	// The `actions` fixture of the upstream suite has this action; its test with every page prerendered calls it.
	subscribe: defineAction({
		input: z.object({ channel: z.string() }),
		handler: async ({ channel }) => {
			return {
				channel,
				subscribeButtonState: 'smashed',
			};
		},
	}),
};
