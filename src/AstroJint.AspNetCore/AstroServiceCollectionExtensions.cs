using AstroJint.AspNetCore;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Microsoft.Extensions.DependencyInjection;

/// <summary>Registers the services that host a built Astro site.</summary>
public static class AstroServiceCollectionExtensions
{
    /// <summary>
    /// Adds the services that run Astro's on-demand pages in Jint. The Astro build is loaded when the host starts,
    /// so a missing or outdated build fails fast. Pair it with <c>UseAstro()</c>, or with
    /// <c>UseAstroStaticFiles()</c> and <c>MapAstro()</c>.
    /// </summary>
    public static IServiceCollection AddAstro(this IServiceCollection services, Action<AstroOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var options = services.AddOptions<AstroOptions>();
        if (configure is not null)
        {
            options.Configure(configure);
        }

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<AstroOptions>, AstroOptionsValidator>());

        // Where Astro.session keeps its data. A cache that the application registers wins, in either order.
        services.AddDistributedMemoryCache();
        services.TryAddSingleton<AstroApp>();
        services.AddHostedService<AstroWarmupService>();
        return services;
    }
}
