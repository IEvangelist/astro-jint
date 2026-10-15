import type { APIRoute } from 'astro';

// 'withSessionEndpoint': a response of a request that used the session sets the session cookie and must not be cached.
export const GET: APIRoute = async (ctx) => {
  ctx.cache.set({ maxAge: 300, tags: ['session'] });
  const count = ((await ctx.session!.get<number>('count')) ?? 0) + 1;
  ctx.session!.set('count', count);
  return Response.json({ count, nonce: Math.random() });
};
