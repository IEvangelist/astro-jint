import type { MiddlewareHandler } from 'astro';

// Answers every path but "/" with an empty 404 response.
export const onRequest: MiddlewareHandler = async (context, next) => {
  if (context.url.pathname !== '/') {
    return new Response(null, { status: 404 });
  }
  return next();
};
