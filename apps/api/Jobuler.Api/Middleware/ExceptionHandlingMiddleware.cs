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
                "אימות הנתונים נכשל.",
                ve.Errors.Select(e => e.ErrorMessage).ToList()),
            UnauthorizedAccessException => (HttpStatusCode.Forbidden, "אין לך הרשאה לבצע פעולה זו.", (List<string>?)[]),
            KeyNotFoundException        => (HttpStatusCode.NotFound, "הפריט המבוקש לא נמצא.", (List<string>?)[]),
            Jobuler.Application.Common.ConflictException => (HttpStatusCode.Conflict, ex.Message, (List<string>?)[]),
            Jobuler.Application.Common.DomainValidationException => ((HttpStatusCode)422, ex.Message, (List<string>?)[]),
            InvalidOperationException   => (HttpStatusCode.BadRequest, ex.Message, (List<string>?)[]),
            ArgumentException           => (HttpStatusCode.BadRequest, ex.Message, (List<string>?)[]),
            // EF unique constraint violations → 409 Conflict
            Microsoft.EntityFrameworkCore.DbUpdateException dbe when dbe.InnerException?.Message.Contains("unique") == true ||
                dbe.InnerException?.Message.Contains("23505") == true ||
                dbe.InnerException?.Message.Contains("duplicate key") == true
                => (HttpStatusCode.Conflict, "רשומה עם שם או מזהה זה כבר קיימת.", (List<string>?)[]),
            // EF check constraint violations → 400 Bad Request
            Microsoft.EntityFrameworkCore.DbUpdateException dbe2 when dbe2.InnerException?.Message.Contains("23514") == true ||
                dbe2.InnerException?.Message.Contains("violates check constraint") == true
                => (HttpStatusCode.BadRequest, ExtractCheckConstraintMessage(dbe2), (List<string>?)[]),
            // All other EF/DB exceptions → 500, never expose DB internals to client
            Microsoft.EntityFrameworkCore.DbUpdateException
                => (HttpStatusCode.InternalServerError, "אירעה שגיאת מסד נתונים. נסה שוב.", (List<string>?)[]),
            _   => (HttpStatusCode.InternalServerError, "אירעה שגיאה בלתי צפויה.", (List<string>?)[])
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
        if (msg.Contains("chk_task_ends_after_starts")) return "זמן הסיום חייב להיות אחרי זמן ההתחלה.";
        if (msg.Contains("chk_task_shift_duration")) return "משך המשמרת חייב להיות לפחות דקה אחת.";
        if (msg.Contains("chk_task_headcount_positive")) return "מספר האנשים הנדרש חייב להיות לפחות 1.";
        if (msg.Contains("chk_task_daily_window_both_or_neither")) return "יש להגדיר גם שעת התחלה וגם שעת סיום יומית, או להשאיר את שניהם ריקים.";
        if (msg.Contains("chk_slot_order")) return "זמן הסיום חייב להיות אחרי זמן ההתחלה.";
        if (msg.Contains("chk_constraint_severity")) return "ערך חומרת אילוץ לא תקין.";
        return "הנתונים מפרים כלל עסקי. בדוק את הקלט.";
    }
}
