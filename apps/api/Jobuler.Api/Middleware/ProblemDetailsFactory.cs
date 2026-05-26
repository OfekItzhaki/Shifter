using Microsoft.AspNetCore.Mvc;

namespace Jobuler.Api.Middleware;

/// <summary>
/// Factory for constructing RFC 7807 ProblemDetails instances with consistent
/// structure: type URI, instance path, traceId extension, and merged custom extensions.
/// </summary>
internal static class ProblemDetailsFactory
{
    private const string BaseTypeUri = "https://docs.jobuler.com/errors/";

    /// <summary>
    /// Creates a fully-populated <see cref="ProblemDetails"/> instance.
    /// </summary>
    /// <param name="context">The current HTTP context (used for request path and trace ID).</param>
    /// <param name="statusCode">The HTTP status code for the error response.</param>
    /// <param name="title">A short, human-readable summary of the problem type.</param>
    /// <param name="detail">A human-readable explanation specific to this occurrence.</param>
    /// <param name="typeSlug">The slug appended to the base documentation URI (e.g. "validation-failed").</param>
    /// <param name="extensions">Optional additional extension properties to include in the response.</param>
    /// <returns>A configured <see cref="ProblemDetails"/> instance ready for serialization.</returns>
    public static ProblemDetails Create(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        string typeSlug,
        IDictionary<string, object?>? extensions = null)
    {
        var problem = new ProblemDetails
        {
            Type = $"{BaseTypeUri}{typeSlug}",
            Title = title,
            Status = statusCode,
            Detail = detail,
            Instance = context.Request.Path
        };

        // Always include traceId for log correlation
        problem.Extensions["traceId"] = context.TraceIdentifier;

        // Merge any additional extensions passed by the caller
        if (extensions is not null)
        {
            foreach (var (key, value) in extensions)
            {
                problem.Extensions[key] = value;
            }
        }

        return problem;
    }
}
