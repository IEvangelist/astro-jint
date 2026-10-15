import type { MiddlewareHandler } from 'astro';

// Sets a local for the error pages, which a rewrite to /404 or /500 runs the middleware for.
export const onRequest: MiddlewareHandler = async (context, next) => {
  if (context.url.pathname.startsWith('/404') || context.url.pathname.startsWith('/500')) {
    context.locals.interjected = 'Interjected';
  }
  return await next();
};
