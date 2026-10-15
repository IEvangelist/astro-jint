import { sequence } from 'astro:middleware';
import type { MiddlewareHandler } from 'astro';

// The second of two chained middleware rewrites /ssr to a prerendered page, which Astro refuses, and /source to a page that is
// rendered on demand.
const first: MiddlewareHandler = async (_context, next) => next();

const second: MiddlewareHandler = async (context, next) => {
  if (context.url.pathname === '/ssr') {
    return context.rewrite('/prerendered');
  }
  if (context.url.pathname === '/source') {
    return context.rewrite('/target');
  }
  return next();
};

export const onRequest = sequence(first, second);
