using Microsoft.AspNetCore.Mvc;

namespace Jobuler.Api.Middleware;

/// <summary>
/// Strips sensitive data from ProblemDetails when running in production.
/// Ensures stack traces, exception type names, and inner exception details
/// are never present regardless of configuration.
/// </summary>
internal static class ProductionSafetyGuard
{
    private static readonly string[] SensitiveKeys =
    [
        "exceptionType",
        "stackTrace",
        "innerException"
    ];

    /// <summary>
    /// Sanitizes a ProblemDetails instance by removing sensitive extension keys
    /// in production environments. In development, all extensions are left intact
    /// for debugging purposes.
    /// </summary>
    /// <param name="problem">The ProblemDetails instance to sanitize.</param>
    /// <param name="env">The host environment to determine production vs development.</param>
    /// <returns>The same ProblemDetails instance (mutated in-place) for fluent usage.</returns>
    public static ProblemDetails Sanitize(ProblemDetails problem, IHostEnvironment env)
    {
        if (!env.IsProduction())
            return problem;

        // In production: strip sensitive keys regardless of any other configuration
        foreach (var key in SensitiveKeys)
        {
            problem.Extensions.Remove(key);
        }

        return problem;
    }
}
