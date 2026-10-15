import type { APIRoute } from 'astro';

// 'invalidatePathEndpoint': drops what is cached for the path /cached.
export const POST: APIRoute = async (ctx) => {
  await ctx.cache.invalidate({ path: '/cached' });
  return Response.json({ invalidated: true });
};
