import type { APIRoute } from 'astro';

export const GET: APIRoute = (ctx) => {
  const prefs = ctx.cookies.get('prefs')!.json();
  return new Response(`<html><body><dl><dt>mode</dt><dd>${prefs.mode}</dd></dl></body></html>`, {
    headers: { 'Content-Type': 'text/html' },
  });
};
