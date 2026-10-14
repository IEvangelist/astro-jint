/**
 * Build contract between the adapter (writer) and the ASP.NET Core host (reader).
 * The host mirrors these shapes in `AstroManifest`; bump `version` on breaking changes.
 */
export const MANIFEST_FILE_NAME = 'astro-jint.json';
export const MANIFEST_VERSION = 1;

export interface AstroJintRoute {
  /** Route pattern as written in the project, for example `/blog/[slug]`. */
  route: string;
  type: 'page' | 'endpoint' | 'redirect' | 'fallback';
  /** `true` when the route is generated at build time and served from `client`. */
  prerender: boolean;
  origin: 'internal' | 'external' | 'project';
}

export interface AstroJintManifest {
  version: typeof MANIFEST_VERSION;
  adapter: { name: string; version: string };
  astroVersion: string | null;
  /** `base` from the Astro config, always starting with `/`. */
  base: string;
  trailingSlash: 'always' | 'never' | 'ignore';
  buildFormat: 'file' | 'directory' | 'preserve';
  /** Folder (inside `client`) that holds the hashed assets, `_astro` by default. */
  assetsDir: string;
  /** Prerendered pages and static assets, relative to the folder that holds the manifest. */
  client: string;
  /** Server bundle folder, relative to the folder that holds the manifest. */
  server: string;
  /** Server entry module, relative to the folder that holds the manifest. */
  entry: string;
  /**
   * `security.allowedDomains` from the Astro config. With entries, the host only trusts a `Host` header that
   * matches one of them (as the Node adapter does) and Astro only applies matching `X-Forwarded-*` headers.
   */
  allowedDomains: AstroJintAllowedDomain[];
  routes: AstroJintRoute[];
}

export interface AstroJintAllowedDomain {
  protocol?: string;
  hostname?: string;
  port?: string;
}
