using System.Text;
using System.Text.Json;

namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// The <c>app-actions</c> fixture and the sites that the tests of the actions share. The sites differ in what upstream set
/// per test app: the base path, the trailing slash, the build format and the size limit of action bodies. All of them turn the
/// origin check off, because the form requests of the tests carry no <c>Origin</c> header (the check has its own tests in
/// <c>ActionOriginCheckTests</c> and <c>CsrfTests</c>).
/// </summary>
internal static class AppActions
{
    public const string Fixture = "app-actions";

    public static readonly (string Name, string Value) JsonType = ("Content-Type", "application/json");

    public static Task<RawResponse> PostJsonAsync(ConformanceSite site, string path, string json) =>
        site.SendRawAsync(path, "POST", Encoding.UTF8.GetBytes(json), JsonType);

    public static Task<RawResponse> PostFormAsync(ConformanceSite site, string path, params FormBodies.Part[] parts) =>
        site.SendRawAsync(path, "POST", FormBodies.Multipart(parts), FormBodies.MultipartType);

    // What the page form-result.astro prints in <pre id="view">: the result of Astro.getActionResult() as JSON.
    public static JsonElement View(RawResponse response) =>
        JsonDocument.Parse(AppRouting.Text(response, "#view")).RootElement;

    // GET /action-api.json?probe=<probe>&... (src/pages/action-api.json.ts): what the helper of astro:actions that the probe
    // runs gave, as JSON. ProbeAtAsync is for a site that has a base path.
    public static Task<JsonElement> ProbeAsync(ConformanceSite site, string probe, params (string Name, string Value)[] args) =>
        ProbeAtAsync(site, "", probe, args);

    public static async Task<JsonElement> ProbeAtAsync(
        ConformanceSite site, string basePath, string probe, params (string Name, string Value)[] args)
    {
        var query = string.Join(
            "&", args.Prepend((Name: "probe", Value: probe)).Select(arg => $"{arg.Name}={Uri.EscapeDataString(arg.Value)}"));
        var response = await site.SendRawAsync($"{basePath}/action-api.json?{query}");
        if (response.StatusCode != 200)
        {
            Assert.Fail($"Status {response.StatusCode}: {response.Text}");
        }

        return JsonDocument.Parse(response.Text).RootElement;
    }

    // What an action of `forms` answers with: the devalue of one string, which is the JSON of its parsed input.
    public static string Shown(RawResponse response) =>
        JsonDocument.Parse(response.Text).RootElement[0].GetString() ?? "";

    public static Task<ConformanceSite> GetActionsSiteAsync(this SiteHost sites) =>
        sites.GetAsync(Fixture, new { security = new { checkOrigin = false } });

    public static Task<ConformanceSite> GetBaseSiteAsync(this SiteHost sites) =>
        sites.GetAsync(Fixture, new { @base = "/base", security = new { checkOrigin = false } });

    public static Task<ConformanceSite> GetTrailingSlashSiteAsync(this SiteHost sites) =>
        sites.GetAsync(Fixture, new { trailingSlash = "always", security = new { checkOrigin = false } });

    // A base with a trailing slash and the 'file' build format: BASE_URL is '/base/' and the paths of the RPC routes get no slash
    // (shouldAppendTrailingSlash is false), where the other sites append it.
    public static Task<ConformanceSite> GetFileFormatSiteAsync(this SiteHost sites) =>
        sites.GetAsync(
            Fixture, new { @base = "/base/", build = new { format = "file" }, security = new { checkOrigin = false } });

    public static Task<ConformanceSite> GetSmallLimitSiteAsync(this SiteHost sites) =>
        sites.GetAsync(Fixture, new { security = new { checkOrigin = false, actionBodySizeLimit = 100 } });

    // The same build as the default site, on a host that has a single engine: the module state of the fixture (the counter of
    // the increment action) is then the same for every request. A test that counts the runs of an action of its own names a
    // variant, which gives it a host that no other test shares.
    public static Task<ConformanceSite> GetSingleEngineSiteAsync(this SiteHost sites, string variant = "single-engine") =>
        sites.GetAsync(
            Fixture, new { security = new { checkOrigin = false } }, variant: variant, configure: options => options.PoolSize = 1);

    // The same build as the default site, on a host whose locals carry an action result that the caller of App.render() made, as
    // a middleware of a form post does. The request header x-action-result picks the result: error-422 (an UNPROCESSABLE_CONTENT
    // error of testAction), data-null (the data null of testAction) or malformed-error (an error body that is not JSON, for the
    // action serializeForm).
    public static Task<ConformanceSite> GetActionResultSiteAsync(this SiteHost sites) =>
        sites.GetAsync(
            Fixture, new { security = new { checkOrigin = false } }, variant: "action-result", configure: options =>
                options.LocalsFactory = context => context.Request.Headers["x-action-result"].ToString() switch
                {
                    "error-422" => Payload("testAction", new
                    {
                        type = "error",
                        status = 422,
                        contentType = "application/json",
                        body = """{"type":"AstroActionError","code":"UNPROCESSABLE_CONTENT","status":422,"message":""}""",
                    }),
                    "data-null" => Payload("testAction", new
                    {
                        type = "data",
                        status = 200,
                        contentType = "application/json+devalue",
                        body = "[null]",
                    }),
                    "malformed-error" => Payload("serializeForm", new
                    {
                        type = "error",
                        status = 500,
                        contentType = "application/json",
                        body = "not valid json",
                    }),
                    _ => null,
                });

    private static object Payload(string actionName, object actionResult) =>
        new { _actionPayload = new { actionName, actionResult } };
}
