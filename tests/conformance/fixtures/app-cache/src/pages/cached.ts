import type { APIRoute } from 'astro';

// 'cached' of app-cache.test.ts: cached for 300 seconds, tagged `data`, with a timestamp that tells a cached body from a new one.
export const GET: APIRoute = (ctx) => {
  ctx.cache.set({ maxAge: 300, swr: 60, tags: ['data'] });
  return Response.json({ timestamp: Date.now() });
};
