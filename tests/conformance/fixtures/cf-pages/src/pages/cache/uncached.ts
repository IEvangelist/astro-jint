export const prerender = false;

// No cache.set() call and no matching routeRule.
export const GET = async () => {
	return Response.json({ ok: true });
};
