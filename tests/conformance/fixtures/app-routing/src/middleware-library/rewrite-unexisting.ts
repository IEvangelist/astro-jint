import type { MiddlewareHandler } from 'astro';

export const onRequest: MiddlewareHandler = async (context, next) => {
  if (context.url.pathname === '/rewrite') {
    return context.rewrite('/unexisting-page');
  }
  return next();
};
