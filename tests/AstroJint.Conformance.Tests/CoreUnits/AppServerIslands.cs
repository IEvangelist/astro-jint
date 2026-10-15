using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Web;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// The <c>app-server-islands</c> fixture and what its tests share: its three builds (the default one, one with a base and
/// <c>trailingSlash: 'always'</c>, one with the CSP and a body size limit for the island endpoint), <c>encryptString()</c> of Astro
/// with the key that the fixture pins, and the script that Astro renders for an island, parsed.
/// </summary>
internal static partial class AppServerIslands
{
    public const string Fixture = "app-server-islands";

    /// <summary>The limit of the build that has one, in bytes (<c>security.serverIslandBodySizeLimit</c>).</summary>
    public const int BodyLimit = 200;

    // astro.config.mjs of the fixture sets it as ASTRO_KEY, because the build runs in another process than the tests.
    private const string AstroKey = "vIXnBp5P+TkrzNkmkCdgwbRmYJZOttak9iJ/WAkULd8=";

    private static readonly object TrailingSlashNever = new { trailingSlash = "never" };

    private static readonly object BaseAndTrailingSlashAlways = new { @base = "/app", trailingSlash = "always" };

    private static readonly object CspAndBodyLimit = new { security = new { csp = true, serverIslandBodySizeLimit = BodyLimit } };

    // The sites have a single engine: they are stateless and the tests only need them to answer, and the host of a build has the
    // bundle of the server and the Jint engines in memory for as long as the run lasts.
    public static Task<ConformanceSite> GetServerIslandsSiteAsync(this SiteHost sites) =>
        sites.GetAsync(Fixture, TrailingSlashNever, variant: "single-engine", configure: options => options.PoolSize = 1);

    public static Task<ConformanceSite> GetServerIslandsBaseSiteAsync(this SiteHost sites) =>
        sites.GetAsync(Fixture, BaseAndTrailingSlashAlways, variant: "single-engine", configure: options => options.PoolSize = 1);

    public static Task<ConformanceSite> GetServerIslandsCspSiteAsync(this SiteHost sites) =>
        sites.GetAsync(Fixture, CspAndBodyLimit, variant: "single-engine", configure: options => options.PoolSize = 1);

    /// <summary>
    /// The wire format of Astro: 12 random bytes as upper case hex, then the Base64 of the cipher text followed by the 16 bytes of the
    /// tag. The associated data binds the text to its purpose and to the island (<c>export:Island</c>, <c>props:Island</c>,
    /// <c>slots:Island</c>).
    /// </summary>
    public static string Encrypt(string plainText, string additionalData)
    {
        var iv = RandomNumberGenerator.GetBytes(12);
        var data = Encoding.UTF8.GetBytes(plainText);
        var cipherText = new byte[data.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(Convert.FromBase64String(AstroKey), tag.Length);
        aes.Encrypt(iv, data, cipherText, tag, Encoding.UTF8.GetBytes(additionalData));

        return Convert.ToHexString(iv) + Convert.ToBase64String([.. cipherText, .. tag]);
    }

    public static string Decrypt(string encrypted, string additionalData)
    {
        var iv = Convert.FromHexString(encrypted[..24]);
        var cipherAndTag = Convert.FromBase64String(encrypted[24..]);
        var cipherText = cipherAndTag[..^16];
        var plainText = new byte[cipherText.Length];

        using var aes = new AesGcm(Convert.FromBase64String(AstroKey), 16);
        aes.Decrypt(iv, cipherText, cipherAndTag[^16..], plainText, Encoding.UTF8.GetBytes(additionalData));

        return Encoding.UTF8.GetString(plainText);
    }

    /// <summary>The encrypted export of an island that has the default export of its module, as the page encrypts it.</summary>
    public static string EncryptedExport(string island) => Encrypt("default", $"export:{island}");

    /// <summary>
    /// The target of the GET request that an island with these props and slots sends (the JSON of each, empty for none), whatever
    /// the page: <c>/_server-islands/&lt;island&gt;?e=…&amp;p=…&amp;s=…</c>. Pass <c>export</c> to send something else than the
    /// default export.
    /// </summary>
    public static string IslandTarget(string island, string propsJson = "", string slotsJson = "", string? export = null)
    {
        var props = propsJson.Length == 0 ? "" : Encrypt(propsJson, $"props:{island}");
        var slots = slotsJson.Length == 0 ? "" : Encrypt(slotsJson, $"slots:{island}");
        return $"/_server-islands/{island}?{Query(export ?? EncryptedExport(island), props, slots)}";
    }

    public static string Query(string export, string props, string slots) =>
        $"e={Uri.EscapeDataString(export)}&p={Uri.EscapeDataString(props)}&s={Uri.EscapeDataString(slots)}";

    /// <summary>The body of the POST request that an island sends: JSON.</summary>
    public static StringContent IslandBody(string island, string propsJson = "", string slotsJson = "") =>
        JsonBody(new
        {
            encryptedComponentExport = EncryptedExport(island),
            encryptedProps = propsJson.Length == 0 ? "" : Encrypt(propsJson, $"props:{island}"),
            encryptedSlots = slotsJson.Length == 0 ? "" : Encrypt(slotsJson, $"slots:{island}"),
        });

    public static StringContent JsonBody(object body) => new(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

    /// <summary>The scripts of the islands of a page, in the order of the page.</summary>
    public static IslandScript[] Islands(string html) =>
        [.. IslandScriptPattern().Matches(html).Select(match => ParseIsland(match.Groups[1].Value, match.Groups[2].Value))];

    /// <summary>The scripts of a page that have no <c>src</c>, as the text between their tags.</summary>
    public static string[] InlineScripts(string html) =>
        [.. InlineScriptPattern().Matches(html).Select(match => match.Groups[1].Value)];

    /// <summary>The CSP source expression for the inline script with this text, as the SHA-256 hash that Astro puts in the policy.</summary>
    public static string Sha256Source(string script) => $"'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(script)))}'";

    private static IslandScript ParseIsland(string id, string script)
    {
        var url = Unquote(FetchUrlPattern().Match(script), "fetch URL");
        if (!script.Contains("method: 'POST'", StringComparison.Ordinal))
        {
            var query = HttpUtility.ParseQueryString(url[(url.IndexOf('?') + 1)..]);
            return new IslandScript(id, script, "GET", url, query["e"] ?? "", query["p"] ?? "", query["s"] ?? "");
        }

        return new IslandScript(
            id,
            script,
            "POST",
            url,
            Unquote(ExportPattern().Match(script), "encryptedComponentExport"),
            Unquote(PropsPattern().Match(script), "encryptedProps"),
            Unquote(SlotsPattern().Match(script), "encryptedSlots"));
    }

    // The values are JSON strings (`stringifyForScript`, which also escapes `<`).
    private static string Unquote(Match match, string what)
    {
        if (!match.Success)
        {
            Assert.Fail($"The script of the island has no {what}.");
        }

        return JsonSerializer.Deserialize<string>(match.Groups[1].Value)!;
    }

    [GeneratedRegex("""<script type="module" data-astro-rerun data-island-id="([^"]+)">(.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex IslandScriptPattern();

    [GeneratedRegex("""<script(?![^>]*\ssrc=)[^>]*>(.*?)</script>""", RegexOptions.Singleline)]
    private static partial Regex InlineScriptPattern();

    [GeneratedRegex("""fetch\(("(?:[^"\\]|\\.)*"),""")]
    private static partial Regex FetchUrlPattern();

    [GeneratedRegex("""encryptedComponentExport: ("(?:[^"\\]|\\.)*")""")]
    private static partial Regex ExportPattern();

    [GeneratedRegex("""encryptedProps: ("(?:[^"\\]|\\.)*")""")]
    private static partial Regex PropsPattern();

    [GeneratedRegex("""encryptedSlots: ("(?:[^"\\]|\\.)*")""")]
    private static partial Regex SlotsPattern();
}

/// <summary>
/// What a page renders for a <c>server:defer</c> component: the script that fetches the island, which is a GET of
/// <c>/_server-islands/&lt;name&gt;</c> with the encrypted export, props and slots in its query, or a POST of the same as JSON when
/// they do not fit in a URL of 2048 characters.
/// </summary>
internal sealed record IslandScript(string Id, string Script, string Method, string Url, string Export, string Props, string Slots)
{
    /// <summary>The URL without its query.</summary>
    public string Path => Url.Split('?')[0];

    /// <summary>The name of the island, the last segment of the path.</summary>
    public string Name => Path.TrimEnd('/').Split('/')[^1];

    /// <summary>What the script does when the browser runs it.</summary>
    public Task<HttpResponseMessage> FetchAsync(ConformanceSite site) =>
        Method == "GET"
            ? site.SendAsync(HttpMethod.Get, Url)
            : site.SendAsync(
                HttpMethod.Post,
                Url,
                AppServerIslands.JsonBody(new { encryptedComponentExport = Export, encryptedProps = Props, encryptedSlots = Slots }));
}
