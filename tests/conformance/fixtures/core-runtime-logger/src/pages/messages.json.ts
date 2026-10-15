// Not part of the upstream fixture: exposes what `src/logger.mjs` recorded inside the engine, since
// the upstream test reads `globalThis` of the test process, which the host does not share with the
// Jint engine. The middleware logs one `missing-in-handler` warning for this very request before this
// endpoint runs; it is left out so a snapshot only holds messages of earlier requests.
import type { APIRoute } from 'astro';

export const prerender = false;

export const GET: APIRoute = ({ url }) => {
	const messages = ((globalThis as any).__astroRuntimeLoggerMessages ??= []) as Array<{ message: string }>;
	let own = -1;
	for (let index = 0; index < messages.length; index++) {
		if (/missing-in-handler/.test(messages[index].message)) {
			own = index;
		}
	}
	const snapshot = messages.filter((_, index) => index !== own);
	if (url.searchParams.has('clear')) {
		messages.length = 0;
	}
	return Response.json(snapshot);
};
