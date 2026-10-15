import assert from 'node:assert/strict';
import { mkdtemp, rm, writeFile } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { after, before, describe, it } from 'node:test';
import { prismForWorkers } from '../dist/prism.js';

const LANGUAGES_MODULE = 'virtual:astro-cloudflare:prism';
const RESOLVED_LANGUAGES_MODULE = `\0${LANGUAGES_MODULE}`;
const PRISM_LOADER = '/site/node_modules/@astrojs/prism/dist/loadLanguages-workerd.js';
const PRISM_HIGHLIGHTER = '/site/node_modules/@astrojs/prism/dist/highlighter.js';

/** The `this` of a hook: `resolve` answers from a table of specifiers and records what it was asked. */
function contextOf(files: Record<string, string> = {}) {
  const calls: { source: string; importer?: string; options?: Record<string, unknown> }[] = [];
  return {
    calls,
    resolve: async (source: string, importer?: string, options?: Record<string, unknown>) => {
      calls.push({ source, importer, options });
      return source in files ? { id: files[source] } : null;
    },
  };
}

describe('prismForWorkers', () => {
  it('runs before the other plugins, which would resolve the default loader first', () => {
    assert.equal(prismForWorkers().enforce, 'pre');
  });

  describe('#prism-loadLanguages', () => {
    it('is the loader for workers that sits next to the loader that the package imports', async () => {
      const context = contextOf({ './loadLanguages-workerd.js': PRISM_LOADER });

      const id = await prismForWorkers().resolveId.call(context, '#prism-loadLanguages', PRISM_HIGHLIGHTER, { isEntry: false });

      assert.equal(id, PRISM_LOADER);
      // `skipSelf` keeps the resolve from coming back to this hook for the same import.
      assert.deepEqual(context.calls, [
        { source: './loadLanguages-workerd.js', importer: PRISM_HIGHLIGHTER, options: { isEntry: false, skipSelf: true } },
      ]);
    });

    it('is left to the package when it has no loader for workers', async () => {
      const id = await prismForWorkers().resolveId.call(contextOf(), '#prism-loadLanguages', PRISM_HIGHLIGHTER, {});

      assert.equal(id, undefined);
    });
  });

  describe('the virtual module of the grammars', () => {
    let folder: string;
    let components: string;

    before(async () => {
      folder = await mkdtemp(path.join(tmpdir(), 'astro-jint-prism-'));
      components = path.join(folder, 'components.json');
      await writeFile(
        components,
        JSON.stringify({ languages: { meta: { path: 'components/prism-{id}' }, css: {}, markup: {}, missing: {}, rust: {} } }),
      );
    });

    after(() => rm(folder, { recursive: true, force: true }));

    it('is resolved to a virtual id', async () => {
      const id = await prismForWorkers().resolveId.call(contextOf(), LANGUAGES_MODULE, PRISM_LOADER, {});

      assert.equal(id, RESOLVED_LANGUAGES_MODULE);
    });

    it('has an import() for each grammar of prismjs, except meta and the ones that are not there', async () => {
      const plugin = prismForWorkers();
      const context = contextOf({
        'prismjs/components.json': components,
        'prismjs/components/prism-css.js': '/prism/components/prism-css.js',
        'prismjs/components/prism-markup.js': '/prism/components/prism-markup.js',
        'prismjs/components/prism-rust.js': '/prism/components/prism-rust.js',
      });
      await plugin.resolveId.call(context, LANGUAGES_MODULE, PRISM_LOADER, {});

      const code = await plugin.load.call(context, RESOLVED_LANGUAGES_MODULE);

      assert.equal(
        code,
        'export const bundledLanguages = { ' +
          '"css": () => import("/prism/components/prism-css.js"), ' +
          '"markup": () => import("/prism/components/prism-markup.js"), ' +
          '"rust": () => import("/prism/components/prism-rust.js") };',
      );
      // prismjs is a dependency of @astrojs/prism, so it is looked up from the file of the package that imports the module.
      assert.ok(context.calls.every((call) => call.importer === PRISM_LOADER));
    });

    it('is an error when prismjs is not found from the file that imports it', async () => {
      const plugin = prismForWorkers();
      const context = contextOf();
      await plugin.resolveId.call(context, LANGUAGES_MODULE, PRISM_LOADER, {});

      await assert.rejects(plugin.load.call(context, RESOLVED_LANGUAGES_MODULE), /prismjs was not found from \/site\/node_modules/);
    });

    it('is an error when nothing has imported it', async () => {
      await assert.rejects(prismForWorkers().load.call(contextOf(), RESOLVED_LANGUAGES_MODULE), /prismjs was not found/);
    });
  });

  it('leaves every other import and module alone', async () => {
    const plugin = prismForWorkers();
    const context = contextOf();

    assert.equal(await plugin.resolveId.call(context, 'prismjs', PRISM_LOADER, {}), undefined);
    assert.equal(await plugin.resolveId.call(context, '#other', PRISM_LOADER, {}), undefined);
    assert.equal(await plugin.load.call(context, '/site/src/pages/index.astro'), undefined);
    assert.deepEqual(context.calls, []);
  });
});
