import { host, type AstroJintHost } from './host.js';

/**
 * The part of an unstorage driver that Astro's session runtime uses. The runtime wraps it with
 * `createStorage`, so unstorage is not imported here and stays out of the bundle when sessions are off.
 * unstorage calls these functions without a receiver, so none of them may depend on `this`.
 */
interface SessionStorageDriver {
  name: string;
  options: Record<string, never>;
  getItem(key: string): Promise<string | null>;
  setItem(key: string, value: string): Promise<void>;
  removeItem(key: string): Promise<void>;
}

type SessionFunction = 'sessionGet' | 'sessionSet' | 'sessionRemove';

function hostFunction<Name extends SessionFunction>(name: Name): NonNullable<AstroJintHost[Name]> {
  const fn = host?.[name];
  if (!fn) {
    throw new Error(
      `Sessions are stored by the ASP.NET Core host, but it does not provide __astroJint.${name}. ` +
        'Run the server bundle through AstroJint.AspNetCore, or configure a session driver of your own.',
    );
  }

  return fn as NonNullable<AstroJintHost[Name]>;
}

/**
 * Keeps session data in the ASP.NET Core host's `IDistributedCache`, so every engine of the pool
 * shares the same sessions, and so does every instance of the app when the cache is distributed.
 */
export default function sessionDriver(): SessionStorageDriver {
  return {
    name: 'astro-adapter-jint',
    options: {},
    getItem: (key) => hostFunction('sessionGet')(key),
    setItem: (key, value) => hostFunction('sessionSet')(key, value),
    removeItem: (key) => hostFunction('sessionRemove')(key),
  };
}
