import { createKey } from '../../../node_modules/astro/dist/core/encryption.js';
import { createComponent, createHeadAndContent, renderComponent, renderTemplate } from 'astro/runtime/server/index.js';
import { renderComponentToString } from 'astro/runtime/server/render/component.js';
import {
  ServerIslandComponent,
  containsServerDirective,
  renderServerIslandRuntime,
} from 'astro/runtime/server/render/server-islands.js';

// The calls of server-islands-render.test.ts that need an SSRResult of their own (a hostile base, an unknown component, a route with
// head propagation), with its stub and its arguments. The bundle of the host runs the very classes that render every server island.
// A case answers what its test asserts on.

async function createStubResult(overrides: Record<string, unknown> = {}): Promise<any> {
  const key = await createKey();
  return {
    cancelled: false,
    base: '/',
    userAssetsBase: undefined,
    styles: new Set(),
    scripts: new Set(),
    links: new Set(),
    componentMetadata: new Map(),
    inlinedScripts: new Map(),
    createAstro() {
      throw new Error('createAstro() not available in unit tests');
    },
    params: {},
    resolve: async (s: string) => s,
    response: { status: 200, statusText: 'OK', headers: new Headers() },
    request: new Request('http://localhost/'),
    renderers: [],
    clientDirectives: new Map(),
    compressHTML: false,
    partial: false,
    pathname: '/',
    cookies: undefined,
    getServerIslandNameMap: async () =>
      new Map([
        ['src/components/Island.astro', 'Island'],
        ['src/components/BigIsland.astro', 'BigIsland'],
      ]),
    trailingSlash: 'never',
    key: Promise.resolve(key),
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
      routeHasPropagation: false,
      pendingSlotEvaluations: [],
      templateDepth: 0,
    },
    shouldInjectCspMetaTags: false,
    cspDestination: 'header',
    cspAlgorithm: 'SHA-256',
    directives: [],
    scriptHashes: [],
    scriptResources: [],
    styleHashes: [],
    styleResources: [],
    isStrictDynamic: false,
    scriptDirective: { resources: [], hashes: [], strictDynamic: false },
    styleDirective: { resources: [], hashes: [] },
    internalFetchHeaders: {},
    ...overrides,
  };
}

/** Minimal props for a server island pointing at Island.astro. */
function islandProps(extra = {}) {
  return {
    'server:component-path': 'src/components/Island.astro',
    'server:component-export': 'default',
    'server:component-directive': 'server',
    'server:defer': true,
    ...extra,
  };
}

function withTimeout<T>(promise: Promise<T>, ms = 2000): Promise<T> {
  return Promise.race([
    promise,
    new Promise<never>((_, reject) => setTimeout(() => reject(new Error(`timed out after ${ms}ms (deadlock)`)), ms)),
  ]);
}

// The factory body is never invoked: server islands short-circuit to a `ServerIslandComponent` before the component itself is rendered.
const Island = createComponent(() => renderTemplate`<p>island</p>`);

/** A propagating component whose head content must reach `extraHead`. */
const StyledContent = createComponent({
  factory: () =>
    createHeadAndContent('<style>.nested-content{}</style>' as any, renderTemplate`<p>styled content</p>`),
  propagation: 'self',
});

export const cases: Record<string, () => unknown> = {
  // containsServerDirective
  'returns true when server:component-directive is present': () =>
    containsServerDirective({ 'server:component-directive': 'server' }),
  'returns false when server:component-directive is absent': () => containsServerDirective({ foo: 'bar' }),
  'returns false for an empty props object': () => containsServerDirective({}),

  // renderServerIslandRuntime
  'returns a <script> tag containing the replaceServerIsland function': () => renderServerIslandRuntime(),
  'escapes </script> sequences inside the runtime script': () => renderServerIslandRuntime(),

  // ServerIslandComponent: getIslandContent()
  'escapes the island URL for script and attribute contexts': async () => {
    const result = await createStubResult({ base: '/"</script><script>test</script>' });
    const component = new ServerIslandComponent(result, islandProps(), {}, 'Island');
    const content = await component.getIslandContent();
    return { content, preload: String(result._metadata.extraHead[0]) };
  },
  'throws when the component path is not in serverIslandNameMap': async () => {
    const result = await createStubResult();
    const props = islandProps({ 'server:component-path': 'src/components/Unknown.astro' });
    const component = new ServerIslandComponent(result, props, {}, 'Unknown');
    try {
      await component.getIslandContent();
      return { threw: false };
    } catch (error) {
      return { threw: true, message: String((error as Error).message) };
    }
  },

  // ServerIslandComponent: init()
  'records CSP script hashes once when called repeatedly': async () => {
    const result = await createStubResult();
    const component = new ServerIslandComponent(result, islandProps(), {}, 'Island');
    await component.init();
    await component.init();
    return result._metadata.extraScriptHashes.length;
  },

  // renderComponentToString() with a server island
  'does not deadlock when rendered inside a pending slot evaluation': async () => {
    // Models an MDX island inside an Astro component slot on a propagation
    // route (#18156): the enclosing slot pre-render is queued in
    // `pendingSlotEvaluations` and is itself awaiting the island render.
    const result = await createStubResult();
    result._metadata.routeHasPropagation = true;
    const enclosingSlot = Promise.resolve().then(() =>
      renderComponentToString(result, 'Island', Island, islandProps()),
    );
    result._metadata.pendingSlotEvaluations.push(enclosingSlot);

    return await withTimeout(enclosingSlot);
  },
  'collects head content from propagating components inside the island slots': async () => {
    const result = await createStubResult();
    result._metadata.routeHasPropagation = true;
    const slots = {
      default: () => renderTemplate`${renderComponent(result, 'StyledContent', StyledContent, {})}`,
    };
    const enclosingSlot = Promise.resolve().then(() =>
      renderComponentToString(result, 'Island', Island, islandProps(), slots),
    );
    result._metadata.pendingSlotEvaluations.push(enclosingSlot);

    await withTimeout(enclosingSlot);
    return result._metadata.extraHead.map(String);
  },
  'initializes each island once, producing one set of CSP script hashes per island': async () => {
    const result = await createStubResult();
    await renderComponentToString(result, 'Island', Island, islandProps());
    const afterFirst = result._metadata.extraScriptHashes.length;

    // A later island render re-runs head collection over every registered
    // propagator, including the first island.
    await renderComponentToString(result, 'Island', Island, islandProps());
    return { afterFirst, afterSecond: result._metadata.extraScriptHashes.length };
  },
};
