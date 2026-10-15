import type { APIRoute } from 'astro';

// 'invalidateEndpoint': drops what is cached under the tag `data` (the entries of /cached).
export const POST: APIRoute = async (ctx) => {
  await ctx.cache.invalidate({ tags: ['data'] });
  return Response.json({ invalidated: true });
};
