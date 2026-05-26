using Microsoft.AspNetCore.Mvc;

namespace Jobuler.Api.Middleware;

/// <summary>
/// Static helper for controllers to produce RFC 7807 ProblemDetails responses
/// with domain-specific extension properties. Internally delegates to
/// <see cref="ProblemDetailsFactory"/> for consistent structure.
/// </summary>
internal static class ProblemDetailsResults
{
    private const string ProblemJsonContentType = "application/problem+json";

    /// <summary>
    /// Creates an <see cref="ObjectResult"/> containing a fully-populated
    /// <see cref="ProblemDetails"/> instance with the correct content type.
    /// </summary>
    /// <param name="context">The current HTTP context (used for request path and trace ID).</param>
    /// <param name="statusCode">The HTTP status code for the error response.</param>
    /// <param name="title">A short, human-readable summary of the problem type.</param>
    /// <param name="detail">A human-readable explanation specific to this occurrence.</param>
    /// <param name="typeSlug">The slug appended to the base documentation URI (e.g. "shift-request-rejected").</param>
    /// <param name="extensions">Optional additional extension properties to include in the response.</param>
    /// <returns>An <see cref="ObjectResult"/> configured with the ProblemDetails payload and correct content type.</returns>
    public static ObjectResult Problem(
        HttpContext context,
        int statusCode,
        string title,
        string detail,
        string typeSlug,
        IDictionary<string, object?>? extensions = null)
    {
        var problemDetails = ProblemDetailsFactory.Create(
            context,
            statusCode,
            title,
            detail,
            typeSlug,
            extensions);

        var result = new ObjectResult(problemDetails)
        {
            StatusCode = statusCode,
            ContentTypes = { ProblemJsonContentType }
        };

        return result;
    }
}
