import type { APIRoute } from 'astro';

export const GET: APIRoute = (ctx) => {
  ctx.cookies.set('another', 'set-in-from');
  ctx.cookies.set('set-in-from', 'yes');
  return ctx.rewrite('/rewrite-target');
};
