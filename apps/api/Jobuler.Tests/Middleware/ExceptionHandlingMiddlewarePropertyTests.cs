// Feature: problem-details-migration
// Property-based tests for ExceptionHandlingMiddleware (Tasks 6.3–6.7)

using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Api.Middleware;
using Jobuler.Application.Common;
using Jobuler.Application.Feedback.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Jobuler.Tests.Middleware;

public class ExceptionHandlingMiddlewarePropertyTests
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger =
        Substitute.For<ILogger<ExceptionHandlingMiddleware>>();

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ── Shared helpers ───────────────────────────────────────────────────────

    private static IHostEnvironment CreateEnvironment(string name)
    {
        var env = Substitute.For<IHostEnvironment>();
        env.EnvironmentName.Returns(name);
        return env;
    }

    private static DefaultHttpContext CreateHttpContext(string path, string traceId)
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = path;
        context.TraceIdentifier = traceId;
        return context;
    }

    private static async Task<JsonDocument> ReadResponseBody(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        return JsonDocument.Parse(body);
    }

    private static async Task<string> ReadResponseBodyRaw(HttpContext context)
    {
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        return await reader.ReadToEndAsync();
    }

    private ExceptionHandlingMiddleware CreateMiddleware(
        RequestDelegate next, IHostEnvironment environment) =>
        new(next, _logger, environment);

    /// <summary>
    /// Generates a random exception from all mapped types plus random unmapped exceptions.
    /// </summary>
    private static Gen<Exception> GenAnyException()
    {
        return Gen.OneOf(
            Arb.Generate<NonEmptyString>().Select(s => (Exception)new ValidationException(
                new[] { new ValidationFailure("Field", s.Get) })),
            Arb.Generate<NonEmptyString>().Select(s => (Exception)new UnauthorizedAccessException(s.Get)),
            Arb.Generate<NonEmptyString>().Select(s => (Exception)new KeyNotFoundException(s.Get)),
            Arb.Generate<NonEmptyString>().Select(s => (Exception)new ConflictException(s.Get)),
            Arb.Generate<NonEmptyString>().Select(s => (Exception)new DomainValidationException(s.Get)),
            Arb.Generate<PositiveInt>().Select(i => (Exception)new RateLimitExceededException(i.Get)),
            Arb.Generate<NonEmptyString>().Select(s => (Exception)new PaymentRequiredException(s.Get)),
            Arb.Generate<NonEmptyString>().Select(s => (Exception)new InvalidOperationException(s.Get)),
            Arb.Generate<NonEmptyString>().Select(s => (Exception)new ArgumentException(s.Get)),
            // Unmapped exception types
            Arb.Generate<NonEmptyString>().Select(s => (Exception)new NotImplementedException(s.Get)),
            Arb.Generate<NonEmptyString>().Select(s => (Exception)new TimeoutException(s.Get))
        );
    }

    /// <summary>
    /// Generates a safe request path (non-empty, starts with /).
    /// </summary>
    private static Gen<string> GenRequestPath()
    {
        return Arb.Generate<NonEmptyString>()
            .Select(s => "/" + Regex.Replace(s.Get, @"[^\w/\-]", "a"));
    }

    /// <summary>
    /// Generates a non-empty trace identifier.
    /// </summary>
    private static Gen<string> GenTraceId()
    {
        return Arb.Generate<NonEmptyString>()
            .Select(s => "trace-" + Regex.Replace(s.Get, @"[^\w\-]", "x"));
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Feature: problem-details-migration, Property 1: Structural Completeness
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// For any exception thrown during request processing, the middleware response
    /// SHALL always contain a valid JSON body with all RFC 7807 fields (type, title,
    /// status, detail, instance), a traceId extension, Content-Type application/problem+json,
    /// type matching the URI pattern, and instance matching the request path.
    /// </summary>
    /// **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5**
    [Property(MaxTest = 100)]
    public Property AllExceptions_ProduceProblemDetailsWithRequiredFields()
    {
        return Prop.ForAll(
            GenAnyException().ToArbitrary(),
            GenRequestPath().ToArbitrary(),
            GenTraceId().ToArbitrary(),
            (exception, path, traceId) =>
            {
                var env = CreateEnvironment("Production");
                var context = CreateHttpContext(path, traceId);
                var middleware = CreateMiddleware(_ => throw exception, env);

                middleware.InvokeAsync(context).GetAwaiter().GetResult();

                // Assert Content-Type
                context.Response.ContentType.Should().Be("application/problem+json");

                // Parse response body
                var doc = ReadResponseBody(context).GetAwaiter().GetResult();
                var root = doc.RootElement;

                // All RFC 7807 fields present
                root.TryGetProperty("type", out var typeProp).Should().BeTrue("type field must be present");
                root.TryGetProperty("title", out var titleProp).Should().BeTrue("title field must be present");
                root.TryGetProperty("status", out var statusProp).Should().BeTrue("status field must be present");
                root.TryGetProperty("detail", out var detailProp).Should().BeTrue("detail field must be present");
                root.TryGetProperty("instance", out var instanceProp).Should().BeTrue("instance field must be present");
                root.TryGetProperty("traceId", out var traceIdProp).Should().BeTrue("traceId extension must be present");

                // type matches URI pattern
                var typeValue = typeProp.GetString()!;
                typeValue.Should().MatchRegex(@"^https://docs\.jobuler\.com/errors/[\w\-]+$");

                // instance matches request path
                instanceProp.GetString().Should().Be(path);

                // traceId matches context TraceIdentifier
                traceIdProp.GetString().Should().Be(traceId);

                // status is a valid HTTP status code
                statusProp.GetInt32().Should().BeInRange(400, 599);

                // title and detail are non-empty strings
                titleProp.GetString().Should().NotBeNullOrEmpty();
                detailProp.GetString().Should().NotBeNullOrEmpty();

                doc.Dispose();
            });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Feature: problem-details-migration, Property 2: Exception-to-Response Mapping Correctness
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// For any exception of a mapped type, the middleware SHALL produce the correct
    /// HTTP status code, title, and detail value as defined in the mapping table.
    /// </summary>
    /// **Validates: Requirements 2.1, 2.2, 2.4, 2.5, 3.1, 3.2, 3.3, 4.1, 4.2, 4.3, 5.1, 5.2, 5.4, 6.1, 6.2, 6.3, 7.1, 7.4, 7.5, 8.1, 8.2, 8.3, 10.1, 10.2, 10.3, 11.1, 11.2, 11.3, 13.1, 13.2, 13.3**
    [Property(MaxTest = 100)]
    public Property MappedExceptions_ProduceCorrectStatusTitleDetail()
    {
        var genMappedException = Gen.OneOf(
            // ValidationException → 400, "Validation Failed", hardcoded detail
            Arb.Generate<NonEmptyString>().Select(s => (
                Exception: (Exception)new ValidationException(
                    new[] { new ValidationFailure("Prop", s.Get) }),
                ExpectedStatus: 400,
                ExpectedTitle: "Validation Failed",
                ExpectedDetail: "אימות הנתונים נכשל.",
                IsDynamic: false
            )),
            // UnauthorizedAccessException → 403, "Forbidden", hardcoded detail
            Arb.Generate<NonEmptyString>().Select(s => (
                Exception: (Exception)new UnauthorizedAccessException(s.Get),
                ExpectedStatus: 403,
                ExpectedTitle: "Forbidden",
                ExpectedDetail: "אין לך הרשאה לבצע פעולה זו.",
                IsDynamic: false
            )),
            // KeyNotFoundException → 404, "Not Found", hardcoded detail
            Arb.Generate<NonEmptyString>().Select(s => (
                Exception: (Exception)new KeyNotFoundException(s.Get),
                ExpectedStatus: 404,
                ExpectedTitle: "Not Found",
                ExpectedDetail: "הפריט המבוקש לא נמצא.",
                IsDynamic: false
            )),
            // ConflictException → 409, "Conflict", dynamic detail
            Arb.Generate<NonEmptyString>().Select(s => (
                Exception: (Exception)new ConflictException(s.Get),
                ExpectedStatus: 409,
                ExpectedTitle: "Conflict",
                ExpectedDetail: s.Get,
                IsDynamic: true
            )),
            // DomainValidationException → 422, "Unprocessable Entity", dynamic detail
            Arb.Generate<NonEmptyString>().Select(s => (
                Exception: (Exception)new DomainValidationException(s.Get),
                ExpectedStatus: 422,
                ExpectedTitle: "Unprocessable Entity",
                ExpectedDetail: s.Get,
                IsDynamic: true
            )),
            // RateLimitExceededException → 429, "Too Many Requests", hardcoded detail
            Arb.Generate<PositiveInt>().Select(i => (
                Exception: (Exception)new RateLimitExceededException(i.Get),
                ExpectedStatus: 429,
                ExpectedTitle: "Too Many Requests",
                ExpectedDetail: "Rate limit exceeded. Try again later.",
                IsDynamic: false
            )),
            // PaymentRequiredException → 402, "Payment Required", dynamic detail
            Arb.Generate<NonEmptyString>().Select(s => (
                Exception: (Exception)new PaymentRequiredException(s.Get),
                ExpectedStatus: 402,
                ExpectedTitle: "Payment Required",
                ExpectedDetail: s.Get,
                IsDynamic: true
            )),
            // InvalidOperationException → 400, "Bad Request", dynamic detail
            // NOTE: Must filter out ConflictException which inherits from InvalidOperationException
            Arb.Generate<NonEmptyString>().Select(s => (
                Exception: (Exception)new InvalidOperationException(s.Get),
                ExpectedStatus: 400,
                ExpectedTitle: "Bad Request",
                ExpectedDetail: s.Get,
                IsDynamic: true
            )),
            // ArgumentException → 400, "Bad Request", dynamic detail
            Arb.Generate<NonEmptyString>().Select(s => (
                Exception: (Exception)new ArgumentException(s.Get),
                ExpectedStatus: 400,
                ExpectedTitle: "Bad Request",
                ExpectedDetail: s.Get,
                IsDynamic: true
            ))
        );

        return Prop.ForAll(
            genMappedException.ToArbitrary(),
            testCase =>
            {
                var env = CreateEnvironment("Production");
                var context = CreateHttpContext("/test/path", "trace-123");
                var middleware = CreateMiddleware(_ => throw testCase.Exception, env);

                middleware.InvokeAsync(context).GetAwaiter().GetResult();

                // Assert status code
                context.Response.StatusCode.Should().Be(testCase.ExpectedStatus,
                    $"exception type {testCase.Exception.GetType().Name} should map to {testCase.ExpectedStatus}");

                // Parse response body
                var doc = ReadResponseBody(context).GetAwaiter().GetResult();
                var root = doc.RootElement;

                // Assert title
                root.GetProperty("title").GetString().Should().Be(testCase.ExpectedTitle);

                // Assert detail
                if (testCase.IsDynamic)
                {
                    root.GetProperty("detail").GetString().Should().Be(testCase.Exception.Message);
                }
                else
                {
                    root.GetProperty("detail").GetString().Should().Be(testCase.ExpectedDetail);
                }

                doc.Dispose();
            });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Feature: problem-details-migration, Property 3: Validation Errors Round-Trip
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// For any set of FluentValidation failures, the errors extension property SHALL
    /// contain a dictionary grouping messages by property name faithfully — no messages
    /// lost, no messages added, no messages assigned to the wrong property.
    /// </summary>
    /// **Validates: Requirements 2.3**
    [Property(MaxTest = 100)]
    public Property ValidationFailures_AreGroupedByPropertyNameFaithfully()
    {
        // Generate a non-empty list of validation failures with random property names and messages
        var genFailure = from propName in Arb.Generate<NonEmptyString>().Select(s => Regex.Replace(s.Get, @"\s+", "Prop"))
                         from message in Arb.Generate<NonEmptyString>().Select(s => s.Get)
                         select new ValidationFailure(propName, message);

        var genFailures = from count in Gen.Choose(1, 10)
                          from failures in Gen.ListOf(count, genFailure)
                          select failures.ToList();

        return Prop.ForAll(
            genFailures.ToArbitrary(),
            failures =>
            {
                var env = CreateEnvironment("Production");
                var context = CreateHttpContext("/test/path", "trace-123");
                var exception = new ValidationException(failures);
                var middleware = CreateMiddleware(_ => throw exception, env);

                middleware.InvokeAsync(context).GetAwaiter().GetResult();

                // Parse response body
                var doc = ReadResponseBody(context).GetAwaiter().GetResult();
                var root = doc.RootElement;

                // Get the errors extension
                root.TryGetProperty("errors", out var errorsProp).Should().BeTrue("errors extension must be present");

                // Build expected grouping from the input failures
                var expected = failures
                    .GroupBy(f => f.PropertyName ?? "")
                    .ToDictionary(g => g.Key, g => g.Select(f => f.ErrorMessage).ToList());

                // Verify each property group
                foreach (var kvp in expected)
                {
                    var propertyName = kvp.Key;
                    var expectedMessages = kvp.Value;

                    errorsProp.TryGetProperty(propertyName, out var propArray).Should().BeTrue(
                        $"property '{propertyName}' should be present in errors dictionary");

                    var actualMessages = propArray.EnumerateArray()
                        .Select(e => e.GetString()!)
                        .ToList();

                    actualMessages.Should().BeEquivalentTo(expectedMessages,
                        $"messages for property '{propertyName}' should match exactly");
                }

                // Verify no extra properties in the errors dictionary
                var actualPropertyCount = 0;
                foreach (var _ in errorsProp.EnumerateObject())
                    actualPropertyCount++;

                actualPropertyCount.Should().Be(expected.Count,
                    "errors dictionary should have exactly the same number of properties as the grouped failures");

                doc.Dispose();
            });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Feature: problem-details-migration, Property 4: Rate Limit Extension Data Preservation
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// For any RateLimitExceededException with a positive RetryAfterSeconds value,
    /// the response SHALL include both a Retry-After HTTP header and a retryAfterSeconds
    /// extension property, both containing the exact numeric value from the exception.
    /// </summary>
    /// **Validates: Requirements 7.2, 7.3**
    [Property(MaxTest = 100)]
    public Property RateLimitException_PreservesRetryAfterInHeaderAndBody()
    {
        return Prop.ForAll(
            Arb.From(Gen.Choose(1, int.MaxValue / 2)),
            retryAfterSeconds =>
            {
                var env = CreateEnvironment("Production");
                var context = CreateHttpContext("/api/feedback", "trace-rate-limit");
                var exception = new RateLimitExceededException(retryAfterSeconds);
                var middleware = CreateMiddleware(_ => throw exception, env);

                middleware.InvokeAsync(context).GetAwaiter().GetResult();

                // Assert Retry-After header
                context.Response.Headers["Retry-After"].ToString()
                    .Should().Be(retryAfterSeconds.ToString(),
                        "Retry-After header must contain the exact RetryAfterSeconds value");

                // Parse response body
                var doc = ReadResponseBody(context).GetAwaiter().GetResult();
                var root = doc.RootElement;

                // Assert retryAfterSeconds extension property
                root.TryGetProperty("retryAfterSeconds", out var retryProp).Should().BeTrue(
                    "retryAfterSeconds extension must be present");
                retryProp.GetInt32().Should().Be(retryAfterSeconds,
                    "retryAfterSeconds extension must contain the exact numeric value");

                doc.Dispose();
            });
    }

    // ═══════════════════════════════════════════════════════════════════════════
    // Feature: problem-details-migration, Property 5: Production Safety — No Sensitive Data Leaks
    // ═══════════════════════════════════════════════════════════════════════════

    /// <summary>
    /// For any exception (including those with stack traces, inner exceptions, and type
    /// metadata) processed in a production environment, the serialized ProblemDetails
    /// response body SHALL never contain the exception's stack trace text, the exception's
    /// type name, or any inner exception message.
    /// </summary>
    /// **Validates: Requirements 8.4, 12.1, 12.2, 12.3, 12.5**
    [Property(MaxTest = 100)]
    public Property ProductionMode_NeverLeaksSensitiveExceptionData()
    {
        // Generate exceptions with inner exceptions and force stack traces
        var genExceptionWithDetails = Arb.Generate<NonEmptyString>().Select(s =>
        {
            var innerMessage = "INNER_SECRET_" + s.Get;
            var inner = new InvalidOperationException(innerMessage);
            Exception thrown;
            try
            {
                throw new ApplicationException("OUTER_" + s.Get, inner);
            }
            catch (Exception ex)
            {
                thrown = ex;
            }
            return thrown;
        });

        return Prop.ForAll(
            genExceptionWithDetails.ToArbitrary(),
            exception =>
            {
                var env = CreateEnvironment("Production");
                var context = CreateHttpContext("/test/path", "trace-prod");
                var middleware = CreateMiddleware(_ => throw exception, env);

                middleware.InvokeAsync(context).GetAwaiter().GetResult();

                // Read raw response body
                var rawBody = ReadResponseBodyRaw(context).GetAwaiter().GetResult();

                // Assert no stack trace text
                if (exception.StackTrace is not null)
                {
                    rawBody.Should().NotContain(exception.StackTrace,
                        "stack trace must never appear in production response");
                }

                // Assert no exception type name
                var typeName = exception.GetType().FullName!;
                rawBody.Should().NotContain(typeName,
                    "exception type name must never appear in production response");

                // Also check short type name
                var shortTypeName = exception.GetType().Name;
                // Only check if it's specific enough (not generic words like "Exception")
                if (shortTypeName != "Exception")
                {
                    rawBody.Should().NotContain(shortTypeName,
                        "exception short type name must never appear in production response");
                }

                // Assert no inner exception message
                if (exception.InnerException is not null)
                {
                    rawBody.Should().NotContain(exception.InnerException.Message,
                        "inner exception message must never appear in production response");
                }
            });
    }
}
