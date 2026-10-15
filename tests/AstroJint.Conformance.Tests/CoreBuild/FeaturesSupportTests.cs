using System.Net;
using System.Text.RegularExpressions;

namespace AstroJint.Conformance.CoreBuild;

/// <summary>
/// Adaptation of <c>packages/astro/test/featuresSupport.test.ts</c>: Astro tells the user when the project uses a feature that
/// the adapter does not support. Upstream builds a project with a test adapter that declares no <c>supportedAstroFeatures</c>
/// and expects an error about the features <c>build.middleware</c> and <c>build.split</c>. Both features were removed from
/// Astro, and both assertions sit in a <c>catch</c> block, so the upstream tests pass when the build does not throw (it does
/// not, Astro only logs these messages). The adapter under test declares <c>i18nDomains</c> as unsupported and the other
/// features it can be asked about as stable, so the first test checks the message that Astro logs for that feature, and that
/// the build is not stopped by it. The second test has no counterpart: nothing is left of <c>build.split</c>.
/// <para>
/// Astro 7 only checks <c>i18nDomains</c> when <c>i18n.domains</c> is set, the output is <c>server</c> and <c>site</c> is not
/// set, but it requires <c>site</c> as soon as a domain is listed. An empty <c>domains</c> record is therefore the one config
/// that makes the check log.
/// </para>
/// </summary>
[Trait("Upstream", "core/featuresSupport.test.ts")]
public class FeaturesSupportTests(SiteHost sites)
{
    [Fact(DisplayName = "should error if the adapter doesn't support edge middleware (reports i18nDomains, the feature the adapter does not support)")]
    public async Task ReportsTheFeatureThatTheAdapterDoesNotSupport()
    {
        var site = await sites.GetAsync("cb-server-entry", new
        {
            i18n = new { locales = new[] { "en", "es" }, defaultLocale = "en", domains = new { } },
        });

        var log = site.BuildLog;

        Assert.Contains("The adapter astro-adapter-jint does not currently support the feature \"i18nDomains\". Your project may not build correctly.", log);
        // The features the adapter supports (server output here) are not reported.
        Assert.Single(Regex.Matches(log, "does not currently support the feature"));
        using var response = await site.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
