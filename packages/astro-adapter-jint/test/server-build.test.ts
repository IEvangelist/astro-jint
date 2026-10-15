import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import jint from '../dist/index.js';

async function viteConfigOf(options?: Parameters<typeof jint>[0], command: 'dev' | 'build' = 'build') {
  const updates: any[] = [];

  await (jint(options).hooks['astro:config:setup'] as any)({
    command,
    config: {
      session: false,
      image: { service: { entrypoint: 'astro/assets/services/noop' }, endpoint: { route: '/_image', entrypoint: undefined } },
    },
    logger: { info: () => {}, warn: () => {}, error: () => {}, debug: () => {} },
    updateConfig: (config: any) => {
      updates.push(config);
      return config;
    },
  });

  return updates.find((update) => 'vite' in update)?.vite;
}

const outputOf = (vite: any) => vite.environments.ssr.build.rolldownOptions.output;

describe('astro-adapter-jint server build', () => {
  it('bundles every dependency, for a runtime that is not Node.js', async () => {
    const vite = await viteConfigOf();

    assert.equal(vite.ssr.noExternal, true);
    assert.equal(vite.ssr.target, 'webworker');
  });

  it('prefers the build that a package ships for workers, and keeps the conditions of a web runtime', async () => {
    const { conditions } = (await viteConfigOf()).ssr.resolve;

    assert.equal(conditions[0], 'worker');
    assert.ok(conditions.includes('module') && conditions.includes('browser'));
    assert.ok(!conditions.includes('node'));
  });

  it('splits the server bundle into chunks, so the manifest and the assets of content collections both reach it', async () => {
    assert.equal(outputOf(await viteConfigOf()).codeSplitting, true);
  });

  it('minifies the server bundle only when asked to', async () => {
    assert.equal(outputOf(await viteConfigOf()).minify, undefined);
    assert.equal(outputOf(await viteConfigOf({ minify: true })).minify, true);
    assert.equal(outputOf(await viteConfigOf({ minify: true })).codeSplitting, true);
  });

  it('leaves the dev server alone', async () => {
    assert.equal(await viteConfigOf(undefined, 'dev'), undefined);
  });
});
