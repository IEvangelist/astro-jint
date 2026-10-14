using Jint.WebApi;
using Microsoft.Extensions.Logging;

namespace AstroJint.AspNetCore;

/// <summary>Sends what the JavaScript <c>console</c> writes to the .NET logging pipeline.</summary>
/// <remarks>Engines call the sink from the thread that runs them, so one instance serves all engines of a site.</remarks>
internal sealed class LoggerConsoleSink(ILogger logger) : ConsoleSink
{
    /// <summary>The category that the logs of the JavaScript console are written under.</summary>
    public const string Category = "AstroJint.Console";

    public override void Write(ConsoleLogLevel level, string message)
    {
        var logLevel = level switch
        {
            ConsoleLogLevel.Debug => LogLevel.Debug,
            ConsoleLogLevel.Warn => LogLevel.Warning,
            ConsoleLogLevel.Error => LogLevel.Error,
            _ => LogLevel.Information,
        };

        if (logger.IsEnabled(logLevel))
        {
            logger.Log(logLevel, "{Message}", message);
        }
    }
}
