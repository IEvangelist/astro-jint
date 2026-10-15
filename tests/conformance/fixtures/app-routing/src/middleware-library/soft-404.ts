import type { MiddlewareHandler } from 'astro';

// Rewrites to the root when the page answers 404 for /soft-404.
export const onRequest: MiddlewareHandler = async (context, next) => {
  const response = await next();
  if (response.status === 404 && context.url.pathname === '/soft-404') {
    return context.rewrite('/');
  }
  return response;
};
