import { existsSync } from 'node:fs';
import path from 'node:path';
import type { AstroJintStaticHeaders } from './manifest.js';

const CONTENT_SECURITY_POLICY = 'Content-Security-Policy';

/** The shape of Astro's `routeToHeaders`, which is keyed by the pathname that a page was prerendered for. */
export type RouteToHeaders = ReadonlyMap<string, { headers: Headers }>;

/**
 * Keeps the Content-Security-Policy that Astro rendered for each prerendered page, and ties it to the page's file in
 * `clientDir`. The host serves those files straight from disk, so it can only look headers up by file, not by route.
 * Like the official adapters, this carries the policy and nothing else: the rest of what a page renders (a
 * `Set-Cookie` of a cookie that was set at build time, say) must not be replayed on every request.
 */
export function collectStaticHeaders(routeToHeaders: RouteToHeaders, clientDir: string): AstroJintStaticHeaders[] {
  const byFile = new Map<string, AstroJintStaticHeaders>();

  for (const [pathname, { headers }] of routeToHeaders) {
    const policy = headers.get(CONTENT_SECURITY_POLICY);
    const file = policy ? findPageFile(clientDir, pathname) : undefined;
    if (policy && file) {
      byFile.set(file, { file, headers: { [CONTENT_SECURITY_POLICY]: policy } });
    }
  }

  return [...byFile.values()];
}

// Which file a pathname ended up in depends on `build.format` and on the kind of route, so look at what exists
// rather than work it out again: `/about` is `about/index.html` or `about.html`, and `/404` is `404.html`. The
// pathname is the name of the file as is, because Astro encodes it to build a URL and decodes that to write it.
function findPageFile(clientDir: string, pathname: string): string | undefined {
  const page = pathname.replace(/^\/+|\/+$/g, '');
  const candidates = page === '' ? ['index.html'] : [`${page}/index.html`, `${page}.html`];
  return candidates.find((candidate) => existsSync(path.join(clientDir, candidate)));
}
