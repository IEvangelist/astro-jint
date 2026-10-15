import { builtinModules } from 'node:module';

/** What is read of Rollup's output bundle: a chunk lists what it imports, bundled files and external modules alike. */
export type OutputBundle = Readonly<
  Record<
    string,
    { type: string; isEntry?: boolean; imports?: readonly string[]; dynamicImports?: readonly string[] }
  >
>;

/** An import that the bundle leaves to its runtime, so it is not a file of the bundle. */
export interface UnresolvableImport {
  /** The file of the bundle that has the import, relative to the server folder. */
  file: string;
  specifier: string;
  dynamic: boolean;
  /**
   * The file is linked when an engine starts: it is an entry or something that an entry imports statically. The other
   * files are loaded by an `import()`, when a request needs them.
   */
  atStart: boolean;
}

/**
 * Finds what the server bundle imports from outside of itself. The host only loads relative imports of the files of
 * the bundle, so each of these fails in Jint: a static import when its file is linked, which keeps every engine from
 * starting if that is when the engine starts, and an `import()` when it runs.
 */
export function findUnresolvableImports(bundle: OutputBundle): UnresolvableImport[] {
  const found: UnresolvableImport[] = [];
  const atStart = filesLinkedAtStart(bundle);

  for (const [file, output] of Object.entries(bundle)) {
    if (output.type !== 'chunk') {
      continue;
    }

    for (const specifier of output.imports ?? []) {
      if (!(specifier in bundle)) {
        found.push({ file, specifier, dynamic: false, atStart: atStart.has(file) });
      }
    }

    for (const specifier of output.dynamicImports ?? []) {
      if (!(specifier in bundle)) {
        found.push({ file, specifier, dynamic: true, atStart: atStart.has(file) });
      }
    }
  }

  return found;
}

/** The entries, and every file that is reached from one of them through static imports only. */
function filesLinkedAtStart(bundle: OutputBundle): Set<string> {
  const reached = new Set<string>();
  const pending = Object.entries(bundle)
    .filter(([, output]) => output.type === 'chunk' && output.isEntry)
    .map(([file]) => file);

  for (let file = pending.pop(); file !== undefined; file = pending.pop()) {
    if (reached.has(file)) {
      continue;
    }

    reached.add(file);
    pending.push(...(bundle[file]?.imports ?? []).filter((specifier) => specifier in bundle));
  }

  return reached;
}

/** One warning for each file of the bundle that has such imports. `folder` is where the bundle is, in the output. */
export function describeUnresolvableImports(found: readonly UnresolvableImport[], folder: string): string[] {
  const files = new Map<string, UnresolvableImport[]>();
  for (const item of found) {
    files.set(item.file, [...(files.get(item.file) ?? []), item]);
  }

  return [...files].map(([file, imports]) => {
    const location = `${folder}/${file}`;
    const statics = unique(imports.filter((item) => !item.dynamic));
    const dynamics = unique(imports.filter((item) => item.dynamic));
    const parts: string[] = [];

    if (statics.length > 0) {
      const consequence = imports.some((item) => item.atStart)
        ? 'so no engine can start: every on-demand route and server island answers 500.'
        : 'so loading this file fails: the on-demand routes and server islands that need it answer 500.';

      parts.push(
        `${location} imports ${statics.join(', ')}, which the host cannot load, ${consequence} ` +
          'Prerendered pages are files and are not affected.',
      );
    }

    if (dynamics.length > 0) {
      parts.push(
        `${location} has import() of ${dynamics.join(', ')}, which the host cannot load, so the import() rejects when it runs.`,
      );
    }

    if (imports.some((item) => isBuiltIn(item.specifier))) {
      parts.push('Jint has no Node.js built-in modules: remove the import, or the code that needs it, from what runs on request.');
    }

    if (imports.some((item) => !isBuiltIn(item.specifier))) {
      parts.push('Jint only loads relative imports of files in the server bundle: bundle the package, or remove the import.');
    }

    return parts.join(' ');
  });
}

/**
 * A Vite plugin that reports the imports that are left in the bundle of the server (the `ssr` environment) to `report`,
 * which is called once for each build of it.
 */
export function reportUnresolvableImports(report: (found: UnresolvableImport[]) => void) {
  return {
    name: 'astro-adapter-jint:server-imports',
    applyToEnvironment: (environment: { name: string }) => environment.name === 'ssr',
    generateBundle(_options: unknown, bundle: OutputBundle) {
      report(findUnresolvableImports(bundle));
    },
  };
}

function isBuiltIn(specifier: string): boolean {
  return specifier.startsWith('node:') || builtinModules.includes(specifier);
}

function unique(imports: readonly UnresolvableImport[]): string[] {
  return [...new Set(imports.map((item) => `'${item.specifier}'`))];
}
