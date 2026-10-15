import { maybeRenderHead, renderHead, templateEnter, templateExit } from 'astro/runtime/server/index.js';

// The call of the first test of head.test.ts ('isolates mutations to render instructions'); the other tests of that file are the
// pages under src/pages/head.

export const cases: Record<string, () => unknown> = {
  'isolates mutations to render instructions': () => {
    const instructions = [
      { create: () => renderHead(), type: 'head' },
      { create: () => maybeRenderHead(), type: 'maybe-head' },
      { create: () => templateEnter({} as any), type: 'template-enter' },
      { create: () => templateExit({} as any), type: 'template-exit' },
    ];
    const originals = instructions.map(({ create }) => create());
    for (const instruction of originals) Reflect.set(instruction, 'type', 'invalid');

    try {
      return {
        actual: instructions.map(({ create }) => create().type),
        expected: instructions.map(({ type }) => type),
      };
    } finally {
      for (let i = 0; i < originals.length; i++) {
        Reflect.set(originals[i], 'type', instructions[i].type);
      }
    }
  },
};
