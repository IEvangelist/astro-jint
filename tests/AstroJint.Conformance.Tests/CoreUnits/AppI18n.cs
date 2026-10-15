namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// The <c>app-i18n</c> fixture. It has no pages of its own: each variant lists the routes it needs in its <c>routes</c> config
/// (see <c>astro.config.mjs</c>), as the upstream i18n tests build a different route table for each group of tests, and the
/// routes point at the few components of <c>src/routes</c>.
/// </summary>
internal static class AppI18n
{
    public const string Fixture = "app-i18n";

    /// <summary>A route to inject; <paramref name="component"/> is the name of a file in <c>src/routes</c>.</summary>
    public static object Route(string pattern, string component = "locale", bool prerender = false) =>
        new { pattern, entrypoint = $"./src/routes/{component}.astro", prerender };

    /// <summary>A catch-all below the prefix of the locale, like upstream's <c>localeCatchAll()</c>.</summary>
    public static object LocaleCatchAll(string locale) => Route($"/{locale}/[...slug]");
}
