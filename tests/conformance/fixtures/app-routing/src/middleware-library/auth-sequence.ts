import { sequence } from 'astro:middleware';
import type { MiddlewareHandler } from 'astro';

// Three chained middleware: the second one rewrites with next(), or with context.rewrite() (which skips the third), and the
// third one sets the locals that the pages show. Upstream keeps the context.rewrite() flag in a variable of the test, which a
// site cannot do (one engine serves many requests); the locals of the request do the same.
const first: MiddlewareHandler = async (_context, next) => next();

const second: MiddlewareHandler = async (context, next) => {
  if (context.url.pathname.includes('/auth')) {
    if (context.url.pathname.includes('/auth/dashboard')) {
      context.locals.contextReroute = true;
      return await context.rewrite('/');
    }
    if (context.url.pathname.includes('/auth/base')) {
      return await next('/');
    }
    if (context.url.pathname.includes('/auth/params')) {
      return next('/?foo=bar');
    }
    if (context.url.pathname.includes('/auth/astro-params')) {
      return next('/auth/1234');
    }
  }
  return next();
};

const third: MiddlewareHandler = async (context, next) => {
  if (context.url.pathname.startsWith('/') && !context.locals.contextReroute) {
    context.locals.auth = 'Third function called';
  }
  if (context.params?.id === '1234') {
    context.locals.auth = 'Params changed';
  }
  return next();
};

export const onRequest = sequence(first, second, third);
