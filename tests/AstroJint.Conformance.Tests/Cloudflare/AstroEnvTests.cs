namespace AstroJint.Conformance.Cloudflare;

/// <summary>
/// Port of <c>packages/integrations/cloudflare/test/astro-env.test.ts</c>, its <c>ssr</c> half: <c>astro:env</c> gives a
/// page the public variables of the site (<c>API_URL</c> for the client, <c>PORT</c> for the server) and the secret
/// <c>API_SECRET</c>, and the secret is also there when an action reads it. The site is the fixture <c>cf-env</c>, which is
/// the upstream fixture (a middleware reads the secret when its module is evaluated) without <c>cloudflare:workers</c>.
/// <para>
/// The only difference is where the values come from. A Cloudflare site declares public variables as wrangler
/// <c>vars</c>, which the adapter copies to the environment of the build, and secrets in <c>.dev.vars</c> or the
/// dashboard, which workerd gives to the worker. Here the public variables are in <c>.env.production</c>, the file that
/// Vite reads for <c>astro build</c> and the way to give the build the same variables, and the secret is an entry of
/// <c>AstroOptions.Env</c> of the host, which is the environment of the bundle at runtime. The assertions are the upstream
/// ones, on the page <c>/</c> and, for the action, on <c>/test</c>.
/// </para>
/// </summary>
[Trait("Upstream", "cloudflare/astro-env.test.ts")]
public class AstroEnvTests(SiteHost sites)
{
    private const string Fixture = "cf-env";
    private const string Secret = "123456789";

    private Task<ConformanceSite> StartAsync() =>
        sites.GetAsync(Fixture, variant: "api-secret", configure: options => options.Env["API_SECRET"] = Secret);

    [Fact(DisplayName = "ssr: client")]
    public async Task Client()
    {
        var site = await StartAsync();

        var html = await site.GetHtmlAsync("/");

        Assert.Contains("https://google.de", html.QuerySelector("#client")!.TextContent);
    }

    [Fact(DisplayName = "ssr: server")]
    public async Task Server()
    {
        var site = await StartAsync();

        var html = await site.GetHtmlAsync("/");

        Assert.Contains("4322", html.QuerySelector("#server")!.TextContent);
    }

    [Fact(DisplayName = "ssr: secret")]
    public async Task SecretOfAPage()
    {
        var site = await StartAsync();

        var html = await site.GetHtmlAsync("/");

        Assert.Contains(Secret, html.QuerySelector("#secret")!.TextContent);
    }

    [Fact(DisplayName = "ssr: action secret")]
    public async Task SecretOfAnAction()
    {
        var site = await StartAsync();

        var html = await site.GetHtmlAsync("/test");

        Assert.Contains(Secret, html.QuerySelector("#secret")!.TextContent);
    }
}
