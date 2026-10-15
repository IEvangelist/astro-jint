namespace AstroJint.Conformance.Cloudflare;

/// <summary>The shared fixture <c>cf-ui</c>, which has the UI frameworks and the styles of the upstream fixtures in one build.</summary>
internal static class CfUi
{
    public const string Fixture = "cf-ui";

    public const string SolidGap =
        "GAP-CF-9: the webworker build resolves solid-js to its browser build, whose renderToString returns undefined, "
        + "so Astro answers 500 'Unable to render Component!'";

    public const string SvelteGap =
        "GAP-CF-8: Function.prototype.toString() has no source text in Jint, so @astrojs/svelte does not recognize "
        + "a component and Astro answers 500 'Unable to render Component!'";
}
