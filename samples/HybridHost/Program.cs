using System.Security.Claims;
using AstroJint.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// Everything under "Astro" in the configuration (appsettings.json, environment variables such as
// Astro__Env__API_TOKEN, ...) becomes an option of the host.
builder.Services.AddOptions<AstroOptions>().BindConfiguration("Astro");
builder.Services.AddAstro(options =>
{
    // Astro.locals of every on-demand page: what ASP.NET Core knows about the request.
    options.LocalsFactory = context => new
    {
        traceId = context.TraceIdentifier,
        user = context.User.Identity?.Name,
    };
});

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme).AddCookie();
builder.Services.AddHealthChecks();

// Behind a proxy, Astro should see the address, host and protocol that the visitor used. Only proxies on the loopback
// interface are trusted until KnownProxies or KnownNetworks say otherwise.
builder.Services.Configure<ForwardedHeadersOptions>(options =>
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto | ForwardedHeaders.XForwardedHost);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseAuthentication();

// ASP.NET Core endpoints take precedence over the Astro site, which only handles what nothing else matches.
app.MapHealthChecks("/healthz");
app.MapGet("/api/host", () => Results.Json(new
{
    framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    serverGarbageCollection = System.Runtime.GCSettings.IsServerGC,

    // What the load test (benchmarks/AstroJint.LoadTest --gc-stats /api/host) reads before and after it measures.
    gc = new
    {
        gen0 = GC.CollectionCount(0),
        gen1 = GC.CollectionCount(1),
        gen2 = GC.CollectionCount(2),
        pauseMs = GC.GetTotalPauseDuration().TotalMilliseconds,
        allocatedBytes = GC.GetTotalAllocatedBytes(),
        heapBytes = GC.GetTotalMemory(forceFullCollection: false),
    },
}));

app.MapGet("/sign-in/{name}", async (string name, HttpContext context) =>
{
    var identity = new ClaimsIdentity([new Claim(ClaimTypes.Name, name)], CookieAuthenticationDefaults.AuthenticationScheme);
    await context.SignInAsync(new ClaimsPrincipal(identity));
    return Results.Redirect("/me");
});

app.MapGet("/sign-out", async (HttpContext context) =>
{
    await context.SignOutAsync();
    return Results.Redirect("/me");
});

app.UseAstro();

app.Run();
