import { readFile } from 'node:fs/promises';

/** What a hook of the plugin calls on `this`, as far as the plugin needs it. */
interface ResolveContext {
  resolve(source: string, importer?: string, options?: Record<string, unknown>): Promise<{ id: string } | null>;
}

// The loader for workers of `@astrojs/prism` imports its grammars from this module. The package asks for it by this name, and
// the Cloudflare adapter provides it under the same one.
const LANGUAGES_MODULE = 'virtual:astro-cloudflare:prism';
const RESOLVED_LANGUAGES_MODULE = `\0${LANGUAGES_MODULE}`;

/**
 * Lets the `Prism` component, and Markdown that is highlighted with Prism, work in the server bundle and in the prerender.
 *
 * The default loader of `@astrojs/prism` reads the grammars with `require.resolve()` and a dynamic `require()`, which a bundle
 * cannot have, and `prismjs` only sets its global `Prism` where there is a `global`, which Jint does not have. The package has a
 * loader for workers (`loadLanguages-workerd.js`) for the `workerd` condition, which takes the grammars from a virtual module
 * where each language is a dynamic import. That condition cannot be added to the build as a whole, because it also selects the
 * WebAssembly build of Shiki, which nothing here can bundle. So the plugin answers the two imports that matter: `#prism-loadLanguages`
 * gets the loader for workers, and the virtual module, which the Cloudflare adapter provides for the same loader, lists the
 * grammars of `prismjs`. A build that does not use Prism never reaches either import.
 */
export function prismForWorkers() {
  // `prismjs` is a dependency of `@astrojs/prism`, not necessarily of the site, so its files are looked up from the file that
  // imports the virtual module.
  let loader: string | undefined;

  return {
    name: 'astro-adapter-jint:prism',
    // Without it the import is resolved first, and the default loader is bundled.
    enforce: 'pre' as const,

    async resolveId(this: ResolveContext, id: string, importer?: string, options: Record<string, unknown> = {}) {
      if (id === '#prism-loadLanguages') {
        // A version of the package without the loader for workers keeps the one it has.
        return importer
          ? (await this.resolve('./loadLanguages-workerd.js', importer, { ...options, skipSelf: true }))?.id
          : undefined;
      }

      if (id === LANGUAGES_MODULE) {
        loader = importer;
        return RESOLVED_LANGUAGES_MODULE;
      }

      return undefined;
    },

    async load(this: ResolveContext, id: string) {
      if (id !== RESOLVED_LANGUAGES_MODULE) {
        return undefined;
      }

      const importer = loader;
      const list = importer === undefined ? null : await this.resolve('prismjs/components.json', importer);
      if (importer === undefined || list === null) {
        throw new Error(`${LANGUAGES_MODULE}: prismjs was not found from ${importer ?? 'the file that imports this module'}.`);
      }

      const { languages } = JSON.parse(await readFile(list.id, 'utf8')) as { languages: Record<string, unknown> };
      const grammars = await Promise.all(
        Object.keys(languages)
          .filter((language) => language !== 'meta')
          .map(async (language) => ({ language, file: await this.resolve(`prismjs/components/prism-${language}.js`, importer) })),
      );

      const entries = grammars.flatMap(({ language, file }) =>
        file ? [`${JSON.stringify(language)}: () => import(${JSON.stringify(file.id)})`] : [],
      );
      return `export const bundledLanguages = { ${entries.join(', ')} };`;
    },
  };
}
