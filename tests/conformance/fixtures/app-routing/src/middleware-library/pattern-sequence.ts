import { sequence } from 'astro:middleware';
import type { MiddlewareHandler } from 'astro';

// The first middleware rewrites, the second one stores the route pattern that it is given after the rewrite.
const first: MiddlewareHandler = async (context, next) => {
  if (context.url.pathname === '/index2') {
    return next('/123/post');
  }
  return next('/destination');
};

const second: MiddlewareHandler = async (context, next) => {
  context.locals.pattern = context.routePattern;
  return next();
};

export const onRequest = sequence(first, second);
