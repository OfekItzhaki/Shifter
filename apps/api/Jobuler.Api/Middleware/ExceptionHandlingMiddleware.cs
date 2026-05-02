using FluentValidation;
using System.Net;
using System.Text.Json;

namespace Jobuler.Api.Middleware;

/// <summary>
/// Converts unhandled exceptions into consistent JSON error responses.
/// Prevents stack traces from leaking to clients in production.
/// </summary>
public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next;
        _logger = logger;
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
        var (statusCode, message, errors) = ex switch
        {
            ValidationException ve => (
                HttpStatusCode.BadRequest,
                "Validation failed.",
                ve.Errors.Select(e => e.ErrorMessage).ToList()),
            UnauthorizedAccessException => (HttpStatusCode.Forbidden, "You do not have permission to perform this action.", (List<string>?)[]),
            KeyNotFoundException        => (HttpStatusCode.NotFound, ex.Message, (List<string>?)[]),
            Jobuler.Application.Common.ConflictException => (HttpStatusCode.Conflict, ex.Message, (List<string>?)[]),
            Jobuler.Application.Common.DomainValidationException => ((HttpStatusCode)422, ex.Message, (List<string>?)[]),
            InvalidOperationException   => (HttpStatusCode.BadRequest, ex.Message, (List<string>?)[]),
            ArgumentException           => (HttpStatusCode.BadRequest, ex.Message, (List<string>?)[]),
            // EF unique constraint violations → 409 Conflict
            Microsoft.EntityFrameworkCore.DbUpdateException dbe when dbe.InnerException?.Message.Contains("unique") == true ||
                dbe.InnerException?.Message.Contains("23505") == true ||
                dbe.InnerException?.Message.Contains("duplicate key") == true
                => (HttpStatusCode.Conflict, "A record with this name or identifier already exists.", (List<string>?)[]),
            // EF check constraint violations → 400 Bad Request
            Microsoft.EntityFrameworkCore.DbUpdateException dbe2 when dbe2.InnerException?.Message.Contains("23514") == true ||
                dbe2.InnerException?.Message.Contains("violates check constraint") == true
                => (HttpStatusCode.BadRequest, ExtractCheckConstraintMessage(dbe2), (List<string>?)[]),
            // All other EF/DB exceptions → 500, never expose DB internals to client
            Microsoft.EntityFrameworkCore.DbUpdateException
                => (HttpStatusCode.InternalServerError, "A database error occurred. Please try again.", (List<string>?)[]),
            _   => (HttpStatusCode.InternalServerError, "An unexpected error occurred.", (List<string>?)[])
        };

        // Always log the full exception server-side
        if (statusCode == HttpStatusCode.InternalServerError)
            _logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
        else
            _logger.LogWarning(ex, "Handled exception: {Message}", ex.Message);

        context.Response.StatusCode = (int)statusCode;
        context.Response.ContentType = "application/json";

        var body = JsonSerializer.Serialize(new
        {
            error = message,
            errors = errors?.Count > 0 ? errors : null
        });
        await context.Response.WriteAsync(body);
    }

    private static string ExtractCheckConstraintMessage(Microsoft.EntityFrameworkCore.DbUpdateException ex)
    {
        var msg = ex.InnerException?.Message ?? "";
        if (msg.Contains("chk_task_ends_after_starts")) return "End time must be after start time.";
        if (msg.Contains("chk_task_shift_duration")) return "Shift duration must be at least 1 minute.";
        if (msg.Contains("chk_task_headcount_positive")) return "Required headcount must be at least 1.";
        if (msg.Contains("chk_task_daily_window_both_or_neither")) return "Daily start time and end time must both be set, or both left empty.";
        if (msg.Contains("chk_slot_order")) return "Slot end time must be after start time.";
        if (msg.Contains("chk_constraint_severity")) return "Invalid constraint severity value.";
        return "The data violates a business rule. Please check your input.";
    }
}
