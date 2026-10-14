import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import { fileURLToPath } from 'node:url';
import jint from '../dist/index.js';

type Command = 'dev' | 'build' | 'preview' | 'sync';

async function runConfigSetup(session: unknown, command: Command = 'build') {
  const logs: string[] = [];
  const updates: any[] = [];

  const integration = jint();
  await (integration.hooks['astro:config:setup'] as any)({
    command,
    config: {
      session,
      image: { service: { entrypoint: 'astro/assets/services/noop' }, endpoint: { route: '/_image', entrypoint: undefined } },
    },
    logger: {
      info: (message: string) => logs.push(message),
      warn: () => {},
      error: () => {},
      debug: () => {},
    },
    updateConfig: (config: any) => {
      updates.push(config);
      return config;
    },
  });

  return { session: updates.find((update) => 'session' in update)?.session, logs };
}

const enablesSessions = (logs: string[]) => logs.some((message) => message.includes('Enabling sessions'));

// Ported from @astrojs/node: test/session-false.test.ts
describe('astro-adapter-jint session: false', () => {
  it('wires no session driver when sessions are disabled', async () => {
    const { session, logs } = await runConfigSetup(false);
    assert.equal(session, undefined, 'the adapter should leave `session: false` untouched');
    assert.equal(enablesSessions(logs), false, 'the adapter should not announce that it enabled sessions');
  });

  it('does not override a driver the user configured', async () => {
    const { session, logs } = await runConfigSetup({ driver: { entrypoint: 'custom-driver' } });
    assert.equal(session, undefined, 'the adapter should leave the session configuration untouched');
    assert.equal(enablesSessions(logs), false);
  });

  it('still wires the host session storage by default', async () => {
    const { session, logs } = await runConfigSetup(undefined);
    assert.ok(session?.driver, 'expected the default driver to be wired');
    assert.equal(logs.includes('Enabling sessions with the distributed cache of the ASP.NET Core host'), true);
  });
});

describe('astro-adapter-jint session defaults', () => {
  it('points a build at the driver that talks to the ASP.NET Core host', async () => {
    const { session } = await runConfigSetup(undefined, 'build');
    assert.ok(session.driver.entrypoint instanceof URL, 'a file URL does not depend on how packages are hoisted');
    assert.match(fileURLToPath(session.driver.entrypoint).replaceAll('\\', '/'), /\/dist\/session-driver\.js$/);
  });

  it('keeps sessions in memory while developing, where there is no host to talk to', async () => {
    const { session, logs } = await runConfigSetup(undefined, 'dev');
    assert.equal(session.driver.entrypoint, 'unstorage/drivers/memory');
    assert.equal(logs.includes('Enabling sessions with in-memory storage'), true);
  });

  it('keeps the cookie and the ttl of a session configuration that has no driver', async () => {
    const { session } = await runConfigSetup({ cookie: 'my-session', ttl: 60 });
    assert.equal(session.cookie, 'my-session');
    assert.equal(session.ttl, 60);
    assert.ok(session.driver);
  });
});
