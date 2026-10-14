import assert from 'node:assert/strict';
import { describe, it } from 'node:test';
import {
  describeUnresolvableImports,
  findUnresolvableImports,
  reportUnresolvableImports,
  type OutputBundle,
  type UnresolvableImport,
} from '../dist/server-imports.js';

const chunk = (imports: string[] = [], dynamicImports: string[] = []) => ({ type: 'chunk', imports, dynamicImports });

describe('findUnresolvableImports', () => {
  it('finds nothing in a bundle that only imports its own files', () => {
    const bundle: OutputBundle = {
      'entry.mjs': chunk(['chunks/a.mjs'], ['chunks/b.mjs']),
      'chunks/a.mjs': chunk(['chunks/b.mjs']),
      'chunks/b.mjs': chunk(),
    };

    assert.deepEqual(findUnresolvableImports(bundle), []);
  });

  it('finds a static import of a Node built-in', () => {
    const bundle: OutputBundle = { 'entry.mjs': chunk(['node:https']) };

    assert.deepEqual(findUnresolvableImports(bundle), [{ file: 'entry.mjs', specifier: 'node:https', dynamic: false }]);
  });

  it('tells a static import from an import()', () => {
    const bundle: OutputBundle = { 'entry.mjs': chunk(['node:fs'], ['left-pad']) };

    assert.deepEqual(findUnresolvableImports(bundle), [
      { file: 'entry.mjs', specifier: 'node:fs', dynamic: false },
      { file: 'entry.mjs', specifier: 'left-pad', dynamic: true },
    ]);
  });

  it('looks at every chunk and skips what is not a chunk', () => {
    const bundle: OutputBundle = {
      'entry.mjs': chunk(['chunks/a.mjs']),
      'chunks/a.mjs': chunk(['node:path']),
      'logo.svg': { type: 'asset' },
    };

    assert.deepEqual(findUnresolvableImports(bundle), [{ file: 'chunks/a.mjs', specifier: 'node:path', dynamic: false }]);
  });
});

describe('describeUnresolvableImports', () => {
  const found = (file: string, specifier: string, dynamic = false): UnresolvableImport => ({ file, specifier, dynamic });

  it('says nothing when there is nothing to report', () => {
    assert.deepEqual(describeUnresolvableImports([], 'server'), []);
  });

  it('names the file, the module and what the host does about it', () => {
    const [message, ...others] = describeUnresolvableImports([found('entry.mjs', 'node:https')], 'server');

    assert.deepEqual(others, []);
    assert.match(message!, /^server\/entry\.mjs imports 'node:https'/);
    assert.match(message!, /no engine can start/);
    assert.match(message!, /answers 500/);
    assert.match(message!, /Prerendered pages are files and are not affected/);
    assert.match(message!, /no Node\.js built-in modules/);
  });

  it('warns once for each file, with all of its modules', () => {
    const messages = describeUnresolvableImports(
      [found('entry.mjs', 'node:fs'), found('entry.mjs', 'node:fs'), found('entry.mjs', 'node:path'), found('chunks/a.mjs', 'node:os')],
      'server',
    );

    assert.equal(messages.length, 2);
    assert.match(messages[0]!, /^server\/entry\.mjs imports 'node:fs', 'node:path',/);
    assert.match(messages[1]!, /^server\/chunks\/a\.mjs imports 'node:os',/);
  });

  it('says that an import() only rejects', () => {
    const [message] = describeUnresolvableImports([found('entry.mjs', 'node:fs', true)], 'server');

    assert.match(message!, /^server\/entry\.mjs has import\(\) of 'node:fs'/);
    assert.match(message!, /the import\(\) rejects when it runs/);
    assert.doesNotMatch(message!, /no engine can start/);
  });

  it('suggests bundling a package that is not a Node built-in', () => {
    const [message] = describeUnresolvableImports([found('entry.mjs', 'left-pad')], 'server');

    assert.match(message!, /bundle the package/);
    assert.doesNotMatch(message!, /no Node\.js built-in modules/);
  });

  it('knows a Node built-in without the node: prefix', () => {
    const [message] = describeUnresolvableImports([found('entry.mjs', 'https')], 'server');

    assert.match(message!, /no Node\.js built-in modules/);
    assert.doesNotMatch(message!, /bundle the package/);
  });
});

describe('reportUnresolvableImports', () => {
  it('reports what is left in the bundle of the server', () => {
    const reported: UnresolvableImport[][] = [];
    const plugin = reportUnresolvableImports((found) => reported.push(found));

    plugin.generateBundle({}, { 'entry.mjs': chunk(['node:https']) });

    assert.deepEqual(reported, [[{ file: 'entry.mjs', specifier: 'node:https', dynamic: false }]]);
  });

  it('only applies to the ssr environment', () => {
    const plugin = reportUnresolvableImports(() => {});

    assert.equal(plugin.applyToEnvironment({ name: 'ssr' }), true);
    assert.equal(plugin.applyToEnvironment({ name: 'client' }), false);
    assert.equal(plugin.applyToEnvironment({ name: 'prerender' }), false);
  });
});
