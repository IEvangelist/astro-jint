import { chunkToString } from 'astro/runtime/server/render/common.js';
import { createRenderInstruction } from 'astro/runtime/server/render/instruction.js';

// The calls of inert-script-dedup.test.ts: each case runs the test's steps and answers what it asserts on.

function createStubResult() {
  return {
    clientDirectives: new Map([['load', 'console.log("directive")']]),
    _metadata: {
      hasHydrationScript: false,
      rendererSpecificHydrationScripts: new Set<string>(),
      hasRenderedHead: false,
      renderedScripts: new Set<string>(),
      hasDirectives: new Set<string>(),
      hasRenderedServerIslandRuntime: false,
      headInTree: false,
      extraHead: [],
      extraStyleHashes: [],
      extraScriptHashes: [],
      propagators: new Set(),
      templateDepth: 0,
    },
  };
}

function directiveInstruction() {
  return createRenderInstruction({
    type: 'directive',
    hydration: {
      directive: 'load',
      value: '',
      componentUrl: '',
      componentExport: { value: '' },
    },
  } as any);
}

export const cases: Record<string, () => unknown> = {
  'does not consume directive or hydration dedup inside templates': () => {
    const result = createStubResult();
    result._metadata.templateDepth = 1;
    const instruction = directiveInstruction();

    const inertOutput = chunkToString(result as any, instruction).toString();
    const inert = {
      output: inertOutput,
      hasHydrationScript: result._metadata.hasHydrationScript,
      hasLoad: result._metadata.hasDirectives.has('load'),
    };

    result._metadata.templateDepth = 0;
    const executableOutput = chunkToString(result as any, instruction).toString();
    return {
      inert,
      executable: {
        output: executableOutput,
        hasHydrationScript: result._metadata.hasHydrationScript,
        hasLoad: result._metadata.hasDirectives.has('load'),
      },
    };
  },

  'does not emit inert directive scripts when already deduplicated': () => {
    const result = createStubResult();
    const instruction = directiveInstruction();

    result._metadata.hasHydrationScript = true;
    result._metadata.hasDirectives.add('load');
    result._metadata.templateDepth = 1;

    return chunkToString(result as any, instruction);
  },

  'does not consume renderer hydration dedup inside templates': () => {
    const result = createStubResult();
    const rendererInstruction = createRenderInstruction({
      type: 'renderer-hydration-script',
      rendererName: 'react',
      render: () => '<script>window.__react = true;</script>',
    } as any);

    result._metadata.templateDepth = 1;
    const inertOutput = chunkToString(result as any, rendererInstruction).toString();
    const inertConsumed = result._metadata.rendererSpecificHydrationScripts.has('react');

    result._metadata.templateDepth = 0;
    const executableOutput = chunkToString(result as any, rendererInstruction).toString();
    return {
      inert: { output: inertOutput, consumed: inertConsumed },
      executable: {
        output: executableOutput,
        consumed: result._metadata.rendererSpecificHydrationScripts.has('react'),
      },
    };
  },

  'does not consume server-island runtime dedup inside templates': () => {
    const result = createStubResult();
    const instruction = createRenderInstruction({ type: 'server-island-runtime' } as any);

    result._metadata.templateDepth = 1;
    const inertOutput = chunkToString(result as any, instruction).toString();
    const inertConsumed = result._metadata.hasRenderedServerIslandRuntime;

    result._metadata.templateDepth = 0;
    const executableOutput = chunkToString(result as any, instruction).toString();
    return {
      inert: { output: inertOutput, consumed: inertConsumed },
      executable: {
        output: executableOutput,
        consumed: result._metadata.hasRenderedServerIslandRuntime,
      },
    };
  },
};
