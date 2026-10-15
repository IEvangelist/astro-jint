namespace AstroJint.Conformance.CoreUnits;

/// <summary>
/// The <c>app-middleware</c> fixture with the host that the middleware tests share. The locals that the host gives to
/// <c>App.render()</c> come from the request header <c>x-name</c> (<c>{ name }</c>, or <c>{}</c> without it), so each request
/// chooses what upstream passed as <c>render(request, { locals: { name } })</c>.
/// </summary>
internal static class AppMiddleware
{
    public const string Fixture = "app-middleware";

    public static Task<ConformanceSite> GetMiddlewareSiteAsync(this SiteHost sites) =>
        sites.GetAsync(Fixture, variant: "name-header", configure: options =>
            options.LocalsFactory = context =>
            {
                var locals = new Dictionary<string, object?>();
                if (context.Request.Headers.TryGetValue("x-name", out var name))
                {
                    locals["name"] = name.ToString();
                }

                return locals;
            });
}
