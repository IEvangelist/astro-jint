using AstroJint.AspNetCore;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace Microsoft.AspNetCore.Builder;

/// <summary>Adds a built Astro site to the request pipeline.</summary>
public static class AstroApplicationBuilderExtensions
{
    /// <summary>
    /// Serves the pages and assets that Astro generated at build time. Requests for anything else, and requests that
    /// another endpoint matches, continue down the pipeline.
    /// </summary>
    public static IApplicationBuilder UseAstroStaticFiles(this IApplicationBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);
        return app.UseMiddleware<AstroStaticFilesMiddleware>();
    }

    /// <summary>
    /// Renders Astro's on-demand pages for every request that no other endpoint handles. The returned builder can
    /// carry conventions that apply to on-demand pages only, such as authorization or rate limiting.
    /// </summary>
    public static IEndpointConventionBuilder MapAstro(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        return endpoints
            .MapFallback("{**path}", context => context.RequestServices.GetRequiredService<AstroApp>().RenderAsync(context))
            .WithDisplayName("Astro")
            .WithMetadata(AstroEndpointMetadata.Instance);
    }

    /// <summary>
    /// Serves the static part of the Astro site and renders the on-demand part:
    /// <see cref="UseAstroStaticFiles"/> followed by <see cref="MapAstro"/>.
    /// </summary>
    public static WebApplication UseAstro(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseAstroStaticFiles();
        app.MapAstro();
        return app;
    }
}
