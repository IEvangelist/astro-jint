using Jint;

namespace AstroJint.AspNetCore;

/// <summary>
/// A JavaScript engine with the Astro server bundle loaded. An engine runs one request at a time, so it can answer
/// the host questions the bundle asks (client address, locals) from <see cref="Context"/>.
/// </summary>
internal sealed class AstroEngineSlot(Engine engine, AstroRequestContext context) : IDisposable
{
    public Engine Engine { get; } = engine;

    public AstroRequestContext Context { get; } = context;

    public void Dispose() => Engine.Dispose();
}

/// <summary>The values of the request that an engine is rendering right now, as seen from JavaScript.</summary>
internal sealed class AstroRequestContext
{
    public string? ClientAddress { get; set; }

    public string? LocalsJson { get; set; }

    public void Clear()
    {
        ClientAddress = null;
        LocalsJson = null;
    }
}
