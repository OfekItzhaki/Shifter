using FluentValidation;
using Jobuler.Application.Common;
using Jobuler.Application.Feedback.Exceptions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net;
using System.Text.Json;

namespace Jobuler.Api.Middleware;

/// <summary>
/// Converts unhandled exceptions into RFC 7807 ProblemDetails responses.
/// Uses ProblemDetailsFactory for consistent structure and ProductionSafetyGuard
/// to strip sensitive data in production environments.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _environment;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment environment)
    {
        _next = next;
        _logger = logger;
        _environment = environment;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception ex)
    {
        var (statusCode, title, detail, typeSlug, extensions) = MapException(ex);

        // Always log the full exception server-side
        if (statusCode == 500)
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
        else
            _logger.LogWarning(ex, "Handled exception: {Message}", ex.Message);

        // If the response has already started, we cannot write to it
        if (context.Response.HasStarted)
        {
            _logger.LogError(ex, "Response has already started, cannot write ProblemDetails for exception: {Message}", ex.Message);
            return;
        }

        try
        {
            // Set Retry-After header for rate limit exceptions
            if (ex is RateLimitExceededException rle)
            {
                context.Response.Headers.Append("Retry-After", rle.RetryAfterSeconds.ToString());
            }

            // Build ProblemDetails via factory
            var problem = ProblemDetailsFactory.Create(
                context,
                statusCode,
                title,
                detail,
                typeSlug,
                extensions);

            // In development mode, add debug extensions before sanitization
            if (_environment.IsDevelopment())
            {
                problem.Extensions["exceptionType"] = ex.GetType().FullName;
                problem.Extensions["stackTrace"] = ex.StackTrace;
                if (ex.InnerException is not null)
                {
                    problem.Extensions["innerException"] = ex.InnerException.Message;
                }
            }

            // Sanitize sensitive data (strips debug info in production)
            ProductionSafetyGuard.Sanitize(problem, _environment);

            // Write response atomically
            context.Response.StatusCode = statusCode;
            context.Response.ContentType = "application/problem+json";
            await context.Response.WriteAsJsonAsync(problem, JsonOptions, contentType: "application/problem+json");
        }
        catch (Exception innerEx)
        {
            // Fallback safety net: if ProblemDetails creation or serialization fails,
            // write a minimal valid JSON response preserving the original status code
            _logger.LogCritical(innerEx, "Failed to write ProblemDetails response for exception: {Message}", ex.Message);

            if (!context.Response.HasStarted)
            {
                context.Response.StatusCode = statusCode;
                context.Response.ContentType = "application/problem+json";
                await context.Response.WriteAsync(
                    $"{{\"type\":\"about:blank\",\"title\":\"Error\",\"status\":{statusCode}}}");
            }
            else
            {
                _logger.LogError(innerEx, "Response has already started during fallback, cannot write minimal ProblemDetails");
            }
        }
    }

    private (int StatusCode, string Title, string Detail, string TypeSlug, IDictionary<string, object?>? Extensions) MapException(Exception ex)
    {
        return ex switch
        {
            ValidationException ve => (
                400,
                "Validation Failed",
                "אימות הנתונים נכשל.",
                "validation-failed",
                new Dictionary<string, object?>
                {
                    ["errors"] = ve.Errors
                        .GroupBy(e => e.PropertyName ?? "")
                        .ToDictionary(
                            g => g.Key,
                            g => g.Select(e => e.ErrorMessage).ToArray())
                }),

            RateLimitExceededException rle => (
                429,
                "Too Many Requests",
                "Rate limit exceeded. Try again later.",
                "rate-limit-exceeded",
                new Dictionary<string, object?>
                {
                    ["retryAfterSeconds"] = rle.RetryAfterSeconds
                }),

            UnauthorizedAccessException => (
                403,
                "Forbidden",
                "אין לך הרשאה לבצע פעולה זו.",
                "forbidden",
                null),

            KeyNotFoundException => (
                404,
                "Not Found",
                "הפריט המבוקש לא נמצא.",
                "not-found",
                null),

            PaymentRequiredException => (
                402,
                "Payment Required",
                ex.Message,
                "payment-required",
                null),

            ConflictException => (
                409,
                "Conflict",
                ex.Message,
                "conflict",
                null),

            DomainValidationException => (
                422,
                "Unprocessable Entity",
                ex.Message,
                "unprocessable-entity",
                null),

            InvalidOperationException => (
                400,
                "Bad Request",
                ex.Message,
                "bad-request",
                null),

            ArgumentException => (
                400,
                "Bad Request",
                ex.Message,
                "bad-request",
                null),

            // EF unique constraint violations → 409 Conflict
            DbUpdateException dbe when dbe.InnerException?.Message.Contains("unique") == true ||
                dbe.InnerException?.Message.Contains("23505") == true ||
                dbe.InnerException?.Message.Contains("duplicate key") == true
                => (
                409,
                "Conflict",
                "רשומה עם שם או מזהה זה כבר קיימת.",
                "conflict",
                null),

            // EF check constraint violations → 409 Conflict
            DbUpdateException dbe2 when dbe2.InnerException?.Message.Contains("23514") == true ||
                dbe2.InnerException?.Message.Contains("violates check constraint") == true
                => (
                409,
                "Conflict",
                ExtractCheckConstraintMessage(dbe2),
                "conflict",
                null),

            // All other EF/DB exceptions → 500, never expose DB internals to client
            DbUpdateException => (
                500,
                "Internal Server Error",
                "אירעה שגיאה בלתי צפויה. נסה שוב מאוחר יותר.",
                "internal-server-error",
                null),

            // Unhandled exceptions → 500
            _ => (
                500,
                "Internal Server Error",
                "אירעה שגיאה בלתי צפויה. נסה שוב מאוחר יותר.",
                "internal-server-error",
                null)
        };
    }

    private static string ExtractCheckConstraintMessage(DbUpdateException ex)
    {
        var msg = ex.InnerException?.Message ?? "";
        if (msg.Contains("chk_task_ends_after_starts")) return "זמן הסיום חייב להיות אחרי זמן ההתחלה.";
        if (msg.Contains("chk_task_shift_duration")) return "משך המשמרת חייב להיות לפחות דקה אחת.";
        if (msg.Contains("chk_task_headcount_positive")) return "מספר האנשים הנדרש חייב להיות לפחות 1.";
        if (msg.Contains("chk_task_daily_window_both_or_neither")) return "יש להגדיר גם שעת התחלה וגם שעת סיום יומית, או להשאיר את שניהם ריקים.";
        if (msg.Contains("chk_slot_order")) return "זמן הסיום חייב להיות אחרי זמן ההתחלה.";
        if (msg.Contains("chk_constraint_severity")) return "ערך חומרת אילוץ לא תקין.";
        return "הנתונים מפרים כלל עסקי. בדוק את הקלט.";
    }
}
