import type { MiddlewareHandler } from 'astro';

/**
 * Middleware handlers keyed by the pathname they answer: a key that ends with '/' matches every pathname below it, any
 * other key only that exact pathname. Each upstream test builds an App with an inline middleware of its own, so each handler
 * is that middleware narrowed to the URLs of its test, and every other request goes through next().
 */
export type Rules = Record<string, MiddlewareHandler>;

export function dispatch(...tables: Rules[]): MiddlewareHandler {
  const rules = tables.flatMap((table) => Object.entries(table));
  return (context, next) => {
    const { pathname } = context.url;
    const match = rules.find(([path]) => (path.endsWith('/') ? pathname.startsWith(path) : pathname === path));
    return match ? match[1](context, next) : next();
  };
}

export const localsOf = (context: { locals: unknown }) => context.locals as Record<string, unknown>;
