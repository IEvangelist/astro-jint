export const prerender = false;

// Explicitly opts out of caching via `cache.set(false)`.
export const GET = async (context: any) => {
	context.cache.set(false);
	return Response.json({ ok: true });
};
