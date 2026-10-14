import type { APIRoute } from 'astro';

export const GET: APIRoute = (ctx) => {
  ctx.cookies.set('test', 'value');
  return Response.json({ hi: 'world' });
};
