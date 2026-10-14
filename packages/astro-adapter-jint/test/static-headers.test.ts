import assert from 'node:assert/strict';
import { mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { after, before, describe, it } from 'node:test';
import { pathToFileURL } from 'node:url';
import jint from '../dist/index.js';
import { collectStaticHeaders } from '../dist/static-headers.js';

const POLICY = "script-src 'self' 'sha256-abc'";

const routeToHeaders = (entries: Record<string, Record<string, string>>) =>
  new Map(Object.entries(entries).map(([pathname, headers]) => [pathname, { headers: new Headers(headers) }]));

describe('collectStaticHeaders', () => {
  let client: string;

  before(() => {
    client = mkdtempSync(path.join(tmpdir(), 'astro-jint-static-headers-'));
    for (const file of [
      'index.html',
      'one/index.html',
      'two.html',
      '404.html',
      'café/index.html',
      'a b/index.html',
      'blog/post.html',
    ]) {
      mkdirSync(path.dirname(path.join(client, file)), { recursive: true });
      writeFileSync(path.join(client, file), '<html></html>');
    }
  });

  after(() => rmSync(client, { recursive: true, force: true }));

  const collect = (entries: Record<string, Record<string, string>>) =>
    collectStaticHeaders(routeToHeaders(entries), client);

  it('ties the policy of a page to its file, whichever build format wrote it', () => {
    const headers = collect({
      '/': { 'Content-Security-Policy': POLICY },
      '/one': { 'Content-Security-Policy': POLICY },
      '/two': { 'Content-Security-Policy': POLICY },
      '/blog/post': { 'Content-Security-Policy': POLICY },
    });

    assert.deepEqual(
      headers.map((entry) => entry.file),
      ['index.html', 'one/index.html', 'two.html', 'blog/post.html'],
    );
    for (const entry of headers) {
      assert.deepEqual(entry.headers, { 'Content-Security-Policy': POLICY });
    }
  });

  it('finds a page that was prerendered with a trailing slash', () => {
    const [entry] = collect({ '/one/': { 'Content-Security-Policy': POLICY } });
    assert.equal(entry?.file, 'one/index.html');
  });

  it('finds the prerendered error pages, which are written as files', () => {
    const [entry] = collect({ '/404': { 'Content-Security-Policy': POLICY } });
    assert.equal(entry?.file, '404.html');
  });

  it('uses the name of the file as is for a path that needs encoding', () => {
    const files = collect({
      '/café': { 'Content-Security-Policy': POLICY },
      '/a b': { 'Content-Security-Policy': POLICY },
    }).map((entry) => entry.file);

    assert.deepEqual(files, ['café/index.html', 'a b/index.html']);
  });

  it('finds the header whatever the case of its name', () => {
    const [entry] = collect({ '/': { 'content-security-policy': POLICY } });
    assert.deepEqual(entry?.headers, { 'Content-Security-Policy': POLICY });
  });

  it('keeps the policy and drops everything else a page rendered', () => {
    const [entry] = collect({
      '/': {
        'Content-Type': 'text/html',
        'Set-Cookie': 'build-time=1',
        'X-Rendered-At': 'build',
        'Content-Security-Policy': POLICY,
      },
    });

    assert.deepEqual(entry?.headers, { 'Content-Security-Policy': POLICY });
  });

  it('skips a page without a policy', () => {
    assert.deepEqual(collect({ '/': { 'Content-Type': 'text/html' } }), []);
  });

  it('skips a page that has no file', () => {
    assert.deepEqual(collect({ '/missing': { 'Content-Security-Policy': POLICY } }), []);
  });
});

describe('astro-adapter-jint staticHeaders', () => {
  let root: string;

  before(() => {
    root = mkdtempSync(path.join(tmpdir(), 'astro-jint-static-headers-build-'));
  });

  after(() => rmSync(root, { recursive: true, force: true }));

  async function build(options: { staticHeaders?: boolean }, csp: boolean) {
    const outDir = mkdtempSync(path.join(root, 'out-'));
    const client = path.join(outDir, 'client');
    mkdirSync(client);
    mkdirSync(path.join(outDir, 'server'));
    writeFileSync(path.join(client, 'index.html'), '<html></html>');
    const url = (folder: string) => pathToFileURL(path.join(outDir, folder) + path.sep);

    const config = {
      outDir: pathToFileURL(outDir + path.sep),
      base: '/',
      trailingSlash: 'ignore',
      build: { format: 'directory', assets: '_astro', serverEntry: 'entry.mjs', client: url('client'), server: url('server') },
      security: { csp: csp ? { algorithm: 'SHA-256' } : false, allowedDomains: [] },
    };

    const integration = jint(options);
    const hooks = integration.hooks as Record<string, (arguments_: unknown) => Promise<void> | void>;
    let adapter: any;
    await hooks['astro:config:done']!({ config, setAdapter: (value: unknown) => (adapter = value) });
    await hooks['astro:routes:resolved']!({ routes: [] });
    await hooks['astro:build:generated']!({
      dir: url('client'),
      routeToHeaders: routeToHeaders({ '/': { 'Content-Security-Policy': POLICY } }),
    });
    await hooks['astro:build:done']!({ logger: { info: () => {} } });

    return { adapter, manifest: JSON.parse(readFileSync(path.join(outDir, 'astro-jint.json'), 'utf8')) };
  }

  it('is off by default', async () => {
    const { adapter } = await build({}, true);
    assert.equal(adapter.adapterFeatures.staticHeaders, false);
  });

  it('tells Astro that the adapter replays the headers of prerendered pages', async () => {
    const { adapter } = await build({ staticHeaders: true }, true);
    assert.equal(adapter.adapterFeatures.staticHeaders, true);
  });

  it('records the policy of the pages in the manifest', async () => {
    const { manifest } = await build({ staticHeaders: true }, true);
    assert.deepEqual(manifest.staticHeaders, [{ file: 'index.html', headers: { 'Content-Security-Policy': POLICY } }]);
  });

  it('records nothing without security.csp, which is the only policy Astro vouches for', async () => {
    const { manifest } = await build({ staticHeaders: true }, false);
    assert.deepEqual(manifest.staticHeaders, []);
  });
});
