# astro-jint

Prototype: run [Astro](https://astro.build) on-demand (SSR) pages inside
[Jint](https://github.com/sebastienros/jint) hosted by ASP.NET Core, next to statically generated pages.

- `packages/astro-adapter-jint` - Astro adapter (npm)
- `src/AstroJint.AspNetCore` - ASP.NET Core integration (NuGet)
- `samples/hybrid` - sample Astro site (static + on-demand pages)
- `samples/HybridHost` - sample ASP.NET Core host
- `spikes/` - throwaway experiments (`astro-app`, `JintSpike`, `JintPerf`), findings in `docs/spikes/`

Work in progress.
