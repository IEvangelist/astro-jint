import type { APIRoute } from 'astro';

// 'optOutEndpoint', and the '/no-cache' route of the CDN tests: opts out of caching with cache.set(false).
export const GET: APIRoute = (ctx) => {
  ctx.cache.set(false);
  return Response.json({ timestamp: Date.now(), cached: false });
};
