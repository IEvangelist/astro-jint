import type { APIRoute } from 'astro';

export const GET: APIRoute = (ctx) =>
  Response.json({
    present: ctx.cookies.has('empty'),
    value: ctx.cookies.get('empty')?.value,
    missing: ctx.cookies.has('missing'),
  });
