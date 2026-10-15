import type { APIRoute } from 'astro';

// 'headCachedEndpoint': only GET is defined; a HEAD request runs it too, but must not be cached.
export const GET: APIRoute = (ctx) => {
  ctx.cache.set({ maxAge: 300, tags: ['head-cached'] });
  return Response.json({ timestamp: Date.now() });
};
