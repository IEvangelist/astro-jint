using System.Net;
using System.Text.Json;

namespace AstroJint.Conformance.Core;

/// <summary>
/// Port of <c>packages/astro/test/env-secret.test.ts</c>. The fixture is <c>astro-env-server-secret</c> of Astro
/// (<c>core-env-secret</c>), which declares one secret, <c>KNOWN_SECRET</c> (a number), and a page that prints it with a secret that the
/// schema does not know, read through <c>getSecret()</c>. Upstream gives the test adapter an <c>env</c> option that tells the runtime how to
/// read the environment. The host does the same through <c>AstroOptions.Env</c>, which the tests set with <c>configure</c> (each such test
/// is its own variant of the site). The fixture builds as an on-demand site (<c>output: 'server'</c>) in every test; upstream builds it with
/// no adapter in the two tests that run a dev server or fail.
/// <list type="bullet">
/// <item><c>works in dev</c> starts a dev server with <c>process.env.KNOWN_SECRET</c> set and requests the page. Here the production
/// build is requested with the secret in <c>AstroOptions.Env</c>: adapted.</item>
/// <item><c>fails if validateSecrets is enabled and secret is not set</c> expects <c>fixture.build()</c> to throw an error whose title is
/// <c>Invalid environment variables.</c> and whose message names <c>KNOWN_SECRET</c>. The harness throws an
/// <see cref="InvalidOperationException"/> that holds the output of the failed build. The CLI prints the name of an Astro error
/// (<c>EnvInvalidVariables</c>, the error whose title is that text) and its message, not its title, so the test looks for the name
/// and for <c>KNOWN_SECRET is missing</c>.</item>
/// </list>
/// </summary>
[Trait("Upstream", "core/env-secret.test.ts")]
public class EnvSecretTests(SiteHost sites)
{
    private const string Fixture = "core-env-secret";

    [Fact(DisplayName = "astro:env secret variables: works in dev")]
    public async Task Secret_IsReadFromTheEnvOfTheHostAtRequestTime()
    {
        var site = await sites.GetAsync(Fixture, variant: "known-secret-5", configure: options => options.Env["KNOWN_SECRET"] = "5");

        using var response = await site.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact(DisplayName = "astro:env secret variables: builds without throwing")]
    public async Task Fixture_BuildsWithoutThrowing()
    {
        var site = await sites.GetAsync(Fixture);

        Assert.True(site.PathExists("server/entry.mjs"), "the build must write the server entry");
    }

    [Fact(DisplayName = "astro:env secret variables: adapter can set how env is retrieved")]
    public async Task AdapterEnv_IsWhereTheSecretsComeFrom()
    {
        var site = await sites.GetAsync(Fixture, variant: "known-and-unknown-secret", configure: options =>
        {
            options.Env["KNOWN_SECRET"] = "123456";
            options.Env["UNKNOWN_SECRET"] = "abc";
        });

        using var response = await site.GetAsync("/");
        var html = await response.ReadHtmlAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var data = JsonDocument.Parse(html.QuerySelector("#data")!.TextContent);
        Assert.Equal(JsonValueKind.Number, data.RootElement.GetProperty("KNOWN_SECRET").ValueKind);
        Assert.Equal(123456, data.RootElement.GetProperty("KNOWN_SECRET").GetDouble());
        Assert.Equal("abc", data.RootElement.GetProperty("UNKNOWN_SECRET").GetString());
    }

    [Fact(DisplayName = "astro:env secret variables: fails if validateSecrets is enabled and secret is not set")]
    public async Task ValidateSecrets_FailsTheBuildWhenASecretIsMissing()
    {
        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sites.GetAsync(Fixture, new { env = new { validateSecrets = true } }));

        Assert.Contains("EnvInvalidVariables", failure.Message);
        Assert.Contains("KNOWN_SECRET is missing", failure.Message);
    }
}
