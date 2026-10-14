using Microsoft.Extensions.Options;

namespace AstroJint.AspNetCore;

internal sealed class AstroOptionsValidator : IValidateOptions<AstroOptions>
{
    public ValidateOptionsResult Validate(string? name, AstroOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.DistPath))
        {
            failures.Add($"{nameof(AstroOptions.DistPath)} must point at the Astro build output.");
        }

        if (options.PoolSize < 1)
        {
            failures.Add($"{nameof(AstroOptions.PoolSize)} must be at least 1.");
        }

        if (options.RequestTimeout != Timeout.InfiniteTimeSpan && options.RequestTimeout <= TimeSpan.Zero)
        {
            failures.Add($"{nameof(AstroOptions.RequestTimeout)} must be positive, or Timeout.InfiniteTimeSpan.");
        }

        if (options.MemoryLimitBytes is <= 0)
        {
            failures.Add($"{nameof(AstroOptions.MemoryLimitBytes)} must be positive, or null for no limit.");
        }

        if (options.MaxRequestBodyBytes < 0)
        {
            failures.Add($"{nameof(AstroOptions.MaxRequestBodyBytes)} must not be negative.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
