import { defineMiddleware } from 'astro:middleware';

// 'can set cookies in a rewritten endpoint request from middleware': the middleware sends /rewrite-me to /rewrite-target.
export const onRequest = defineMiddleware(async (context, next) => {
  if (context.url.pathname === '/rewrite-me') {
    return next('/rewrite-target');
  }
  return next();
});
