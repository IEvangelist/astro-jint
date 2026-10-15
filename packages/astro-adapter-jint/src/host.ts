/**
 * What the ASP.NET Core host exposes on `globalThis.__astroJint` before the server bundle is evaluated.
 * Everything is optional so the bundle also runs outside the host, for example in a smoke test.
 * The per-request members describe the request the engine is handling right now; an engine only
 * ever handles one request at a time.
 */
export interface AstroJintHost {
  /** IP address of the connected client, for `Astro.clientAddress`. */
  clientAddress?(): string | null;
  /** `Astro.locals` for the current request, serialized as a JSON object. */
  locals?(): string | null;
  /** Value of an `astro:env` variable, or `null` when it is not defined. */
  env?(name: string): string | null;
  /** Text of a file below `dist/client`, addressed by its URL path, or `null` when there is none. */
  readClientFile?(pathname: string): string | null;
  /** Bytes of a file below `dist/client`, addressed like `readClientFile`, or `null` when there is none. */
  readClientBytes?(pathname: string): ArrayBuffer | null;
  /**
   * The response headers recorded at build time for a file below `dist/client` (see the `staticHeaders` option),
   * addressed like `readClientFile` and serialized as a JSON object, or `null` when there are none.
   */
  readClientHeaders?(pathname: string): string | null;
  /** Lowercase hex SHA-256 of an `ArrayBuffer`, computed by the host because an interpreter is slow at it. */
  sha256?(data: ArrayBuffer): string | null;
  /** The data stored for a session id, or `null` when there is none. Backed by the host's `IDistributedCache`. */
  sessionGet?(id: string): Promise<string | null>;
  /** Stores the data of a session. */
  sessionSet?(id: string, value: string): Promise<void>;
  /** Removes the data of a session. */
  sessionRemove?(id: string): Promise<void>;
}

export const host = (globalThis as { __astroJint?: AstroJintHost }).__astroJint;
