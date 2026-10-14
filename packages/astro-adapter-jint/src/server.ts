import { createApp } from 'astro/app/entrypoint';
import { setGetEnv } from 'astro/env/setup';

/**
 * What the ASP.NET Core host exposes on `globalThis.__astroJint` before this module is evaluated.
 * Everything is optional so the bundle also runs outside the host, for example in a smoke test.
 * The per-request members describe the request the engine is handling right now; an engine only
 * ever handles one request at a time.
 */
interface AstroJintHost {
  /** IP address of the connected client, for `Astro.clientAddress`. */
  clientAddress?(): string | null;
  /** `Astro.locals` for the current request, serialized as a JSON object. */
  locals?(): string | null;
  /** Value of an `astro:env` variable, or `null` when it is not defined. */
  env?(name: string): string | null;
  /** Text of a file below `dist/client`, addressed by its URL path, or `null` when there is none. */
  readClientFile?(pathname: string): string | null;
}

const host = (globalThis as { __astroJint?: AstroJintHost }).__astroJint;

if (host?.env) {
  setGetEnv((name) => host.env!(name) ?? undefined);
}

const app = createApp({ streaming: false });

// Prerendered 404/500 pages live in dist/client. The default implementation fetches them over
// the network, which a sandboxed engine cannot do, so the host reads them from disk instead.
async function fetchPrerenderedErrorPage(url: string): Promise<Response> {
  const html = host!.readClientFile!(new URL(url).pathname);
  if (html === null || html === undefined) {
    return new Response(null, { status: 404 });
  }
  return new Response(html, { headers: { 'content-type': 'text/html; charset=utf-8' } });
}

const PRERENDERED_FILE_ROUTE_TYPES = new Set(['page', 'endpoint']);

// Redirects configured for a static site are prerendered routes, but they have no HTML file
// (`build.redirects` is off), so they must be answered here with a real 3xx. A prerendered page
// or endpoint is a file the host serves before it ever gets here, so only on-demand routes count.
function matchRoute(request: Request) {
  const route = app.match(request, true);
  return route?.prerender && PRERENDERED_FILE_ROUTE_TYPES.has(route.type) ? app.match(request) : route;
}

export default {
  async fetch(request: Request): Promise<Response> {
    const locals = host?.locals?.();
    const response = await app.render(request, {
      addCookieHeader: true,
      clientAddress: host?.clientAddress?.() ?? undefined,
      locals: locals ? JSON.parse(locals) : undefined,
      prerenderedErrorPageFetch: host?.readClientFile ? fetchPrerenderedErrorPage : undefined,
      routeData: matchRoute(request),
    });

    // Jint's fetch handler hosting cannot read stream-backed bodies and Astro re-wraps
    // `response.body` when it merges error pages, so every body is buffered here.
    if (response.body === null) {
      return response;
    }
    return new Response(await response.arrayBuffer(), {
      status: response.status,
      statusText: response.statusText,
      headers: response.headers,
    });
  },
};
