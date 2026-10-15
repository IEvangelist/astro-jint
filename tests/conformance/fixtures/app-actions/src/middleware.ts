import { defineMiddleware } from 'astro:middleware';

// The guard of 'Actions RPC middleware handling' in actions-app.test.ts: the `locked` action answers 401 unless the request
// has the cookie `actionCookie`. Every other request goes on.
export const onRequest = defineMiddleware(async (context, next) => {
  if (context.url.pathname.includes('/_actions/locked') && !context.cookies.has('actionCookie')) {
    return new Response('Unauthorized', { status: 401 });
  }
  return next();
});
