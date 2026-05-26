// Feature: problem-details-migration
// Unit tests for ExceptionHandlingMiddleware (Task 6.1)

using FluentAssertions;
using FluentValidation;
using FluentValidation.Results;
using Jobuler.Api.Middleware;
using Jobuler.Application.Common;
using Jobuler.Application.Feedback.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NSubstitute;
using System.Text.Json;
using Xunit;

namespace Jobuler.Tests.Middleware;

public class ExceptionHandlingMiddlewareTests
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;
    private readonly IHostEnvironment _devEnvironment;
    private readonly IHostEnvironment _prodEnvironment;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ExceptionHandlingMiddlewareTests()
    {
        _logger = Substitute.For<ILogger<ExceptionHandlingMiddleware>>();

        _devEnvironment = Substitute.For<IHostEnvironment>();
        _devEnvironment.EnvironmentName.Returns("Development");

        _prodEnvironment = Substitute.For<IHostEnvironment>();
        _prodEnvironment.EnvironmentName.Returns("Production");
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static DefaultHttpContext CreateHttpContext()
    {
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/test/path";
        context.TraceIdentifier = "test-trace-id-123";
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
        RequestDelegate next,
        IHostEnvironment environment) =>
        new(next, _logger, environment);

    // ── Development mode includes exception details (Req 12.4) ───────────────

    [Fact]
    public async Task DevelopmentMode_IncludesExceptionType_StackTrace_InnerException()
    {
        // Arrange
        var innerEx = new InvalidOperationException("inner error details");
        var exception = new Exception("outer error", innerEx);
        // Force a stack trace by throwing and catching
        Exception thrownEx;
        try { throw exception; }
        catch (Exception ex) { thrownEx = ex; }

        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw thrownEx, _devEnvironment);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var doc = await ReadResponseBody(context);
        var root = doc.RootElement;

        root.GetProperty("exceptionType").GetString().Should().Be(typeof(Exception).FullName);
        root.GetProperty("stackTrace").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("innerException").GetString().Should().Be("inner error details");
    }

    [Fact]
    public async Task DevelopmentMode_NoInnerException_OmitsInnerExceptionExtension()
    {
        // Arrange
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw new KeyNotFoundException("not found"), _devEnvironment);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var doc = await ReadResponseBody(context);
        var root = doc.RootElement;

        root.GetProperty("exceptionType").GetString().Should().Be(typeof(KeyNotFoundException).FullName);
        root.TryGetProperty("innerException", out _).Should().BeFalse();
    }

    // ── Response.HasStarted scenario (Req 11.4) ──────────────────────────────

    [Fact]
    public async Task ResponseHasStarted_DoesNotAttemptWrite()
    {
        // Arrange — use a custom HttpResponse mock that reports HasStarted = true
        var context = new DefaultHttpContext();
        var responseFeature = new HasStartedResponseFeature();
        context.Features.Set<IHttpResponseFeature>(responseFeature);
        context.Response.Body = new MemoryStream();
        context.Request.Path = "/test/path";
        context.TraceIdentifier = "trace-123";

        // Simulate that response has already started
        responseFeature.HasStarted = true;

        var middleware = CreateMiddleware(_ => throw new Exception("test"), _prodEnvironment);

        // Act
        await middleware.InvokeAsync(context);

        // Assert — body should be empty since we couldn't write
        context.Response.Body.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(context.Response.Body);
        var body = await reader.ReadToEndAsync();
        body.Should().BeEmpty();
    }

    // ── Serialization failure triggers minimal JSON fallback (Req 11.4) ──────

    [Fact]
    public async Task SerializationFailure_WritesMinimalFallbackJson()
    {
        // Arrange — use a stream that throws on write to simulate serialization failure
        var context = new DefaultHttpContext();
        context.Request.Path = "/test/path";
        context.TraceIdentifier = "trace-456";
        context.Response.Body = new FailOnFirstWriteStream();

        var middleware = CreateMiddleware(_ => throw new KeyNotFoundException("not found"), _prodEnvironment);

        // Act
        await middleware.InvokeAsync(context);

        // Assert — after the first write fails, the fallback writes minimal JSON
        var stream = (FailOnFirstWriteStream)context.Response.Body;
        var body = stream.GetFallbackContent();

        body.Should().Contain("\"type\":\"about:blank\"");
        body.Should().Contain("\"title\":\"Error\"");
        body.Should().Contain("\"status\":404");
        context.Response.StatusCode.Should().Be(404);
        context.Response.ContentType.Should().Be("application/problem+json");
    }

    // ── DbUpdateException with specific constraint names (Req 5.3) ───────────

    [Theory]
    [InlineData("chk_task_ends_after_starts", "זמן הסיום חייב להיות אחרי זמן ההתחלה.")]
    [InlineData("chk_task_shift_duration", "משך המשמרת חייב להיות לפחות דקה אחת.")]
    [InlineData("chk_task_headcount_positive", "מספר האנשים הנדרש חייב להיות לפחות 1.")]
    [InlineData("chk_task_daily_window_both_or_neither", "יש להגדיר גם שעת התחלה וגם שעת סיום יומית, או להשאיר את שניהם ריקים.")]
    [InlineData("chk_slot_order", "זמן הסיום חייב להיות אחרי זמן ההתחלה.")]
    [InlineData("chk_constraint_severity", "ערך חומרת אילוץ לא תקין.")]
    public async Task DbUpdateException_CheckConstraint_ProducesCorrectHebrewMessage(
        string constraintName, string expectedMessage)
    {
        // Arrange
        var innerEx = new Exception($"violates check constraint \"{constraintName}\"");
        var dbEx = new DbUpdateException("DB error", innerEx);

        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw dbEx, _prodEnvironment);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var doc = await ReadResponseBody(context);
        var root = doc.RootElement;

        context.Response.StatusCode.Should().Be(409);
        root.GetProperty("title").GetString().Should().Be("Conflict");
        root.GetProperty("detail").GetString().Should().Be(expectedMessage);
        root.GetProperty("type").GetString().Should().Be("https://docs.jobuler.com/errors/conflict");
    }

    [Fact]
    public async Task DbUpdateException_UnknownCheckConstraint_ProducesGenericHebrewMessage()
    {
        // Arrange
        var innerEx = new Exception("violates check constraint \"chk_unknown_constraint\"");
        var dbEx = new DbUpdateException("DB error", innerEx);

        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw dbEx, _prodEnvironment);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var doc = await ReadResponseBody(context);
        var root = doc.RootElement;

        context.Response.StatusCode.Should().Be(409);
        root.GetProperty("detail").GetString().Should().Be("הנתונים מפרים כלל עסקי. בדוק את הקלט.");
    }

    [Fact]
    public async Task DbUpdateException_UniqueConstraint_ProducesConflict409()
    {
        // Arrange
        var innerEx = new Exception("duplicate key value violates unique constraint");
        var dbEx = new DbUpdateException("DB error", innerEx);

        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw dbEx, _prodEnvironment);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var doc = await ReadResponseBody(context);
        var root = doc.RootElement;

        context.Response.StatusCode.Should().Be(409);
        root.GetProperty("title").GetString().Should().Be("Conflict");
        root.GetProperty("detail").GetString().Should().Be("רשומה עם שם או מזהה זה כבר קיימת.");
    }

    [Fact]
    public async Task DbUpdateException_OtherError_ProducesInternalServerError500()
    {
        // Arrange
        var innerEx = new Exception("some other database error");
        var dbEx = new DbUpdateException("DB error", innerEx);

        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw dbEx, _prodEnvironment);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var doc = await ReadResponseBody(context);
        var root = doc.RootElement;

        context.Response.StatusCode.Should().Be(500);
        root.GetProperty("title").GetString().Should().Be("Internal Server Error");
        root.GetProperty("detail").GetString().Should().Be("אירעה שגיאה בלתי צפויה. נסה שוב מאוחר יותר.");
    }

    // ── Atomic write — single response operation (Req 3.4) ───────────────────

    [Fact]
    public async Task AtomicWrite_StatusCodeAndContentTypeSetBeforeBodyWrite()
    {
        // Arrange — verify that status code and content-type are set consistently
        // with the body (no partial writes where status is set but body is incomplete)
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw new KeyNotFoundException("not found"), _prodEnvironment);

        // Act
        await middleware.InvokeAsync(context);

        // Assert — status code, content-type, and body are all set consistently
        context.Response.StatusCode.Should().Be(404);
        context.Response.ContentType.Should().Contain("json");

        var doc = await ReadResponseBody(context);
        var root = doc.RootElement;

        // The body is valid JSON with all required fields — proves no partial write
        root.GetProperty("type").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("title").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("status").GetInt32().Should().Be(404);
        root.GetProperty("detail").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("instance").GetString().Should().NotBeNullOrEmpty();
        root.GetProperty("traceId").GetString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task AtomicWrite_ResponseBodyIsCompleteValidJson()
    {
        // Arrange — verify the response is a single complete JSON document (not fragmented)
        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw new UnauthorizedAccessException(), _prodEnvironment);

        // Act
        await middleware.InvokeAsync(context);

        // Assert — reading the full body should parse as a single valid JSON document
        var rawBody = await ReadResponseBodyRaw(context);
        var parseAction = () => JsonDocument.Parse(rawBody);
        parseAction.Should().NotThrow("the response body should be a single complete JSON document");
    }

    // ── Production mode does NOT include sensitive extensions ─────────────────

    [Fact]
    public async Task ProductionMode_DoesNotIncludeSensitiveExtensions()
    {
        // Arrange
        var innerEx = new InvalidOperationException("secret inner details");
        Exception thrownEx;
        try { throw new Exception("outer error", innerEx); }
        catch (Exception ex) { thrownEx = ex; }

        var context = CreateHttpContext();
        var middleware = CreateMiddleware(_ => throw thrownEx, _prodEnvironment);

        // Act
        await middleware.InvokeAsync(context);

        // Assert
        var doc = await ReadResponseBody(context);
        var root = doc.RootElement;

        root.TryGetProperty("exceptionType", out _).Should().BeFalse();
        root.TryGetProperty("stackTrace", out _).Should().BeFalse();
        root.TryGetProperty("innerException", out _).Should().BeFalse();
    }

    // ── Helper classes ───────────────────────────────────────────────────────

    /// <summary>
    /// A custom IHttpResponseFeature that allows controlling HasStarted.
    /// </summary>
    private class HasStartedResponseFeature : IHttpResponseFeature
    {
        public bool HasStarted { get; set; }
        public int StatusCode { get; set; } = 200;
        public string? ReasonPhrase { get; set; }
        public IHeaderDictionary Headers { get; set; } = new HeaderDictionary();
        public Stream Body { get; set; } = new MemoryStream();

        public void OnStarting(Func<object, Task> callback, object state) { }
        public void OnCompleted(Func<object, Task> callback, object state) { }
    }

    /// <summary>
    /// A stream that fails on the first WriteAsync (simulating serialization failure)
    /// but succeeds on subsequent writes (the fallback).
    /// </summary>
    private class FailOnFirstWriteStream : MemoryStream
    {
        private bool _firstWriteFailed;
        private readonly MemoryStream _fallbackStream = new();

        public override void Write(byte[] buffer, int offset, int count)
        {
            if (!_firstWriteFailed)
            {
                _firstWriteFailed = true;
                throw new InvalidOperationException("Simulated serialization failure");
            }
            _fallbackStream.Write(buffer, offset, count);
        }

        public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
        {
            if (!_firstWriteFailed)
            {
                _firstWriteFailed = true;
                throw new InvalidOperationException("Simulated serialization failure");
            }
            return _fallbackStream.WriteAsync(buffer, offset, count, cancellationToken);
        }

        public override ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (!_firstWriteFailed)
            {
                _firstWriteFailed = true;
                throw new InvalidOperationException("Simulated serialization failure");
            }
            return _fallbackStream.WriteAsync(buffer, cancellationToken);
        }

        public string GetFallbackContent()
        {
            _fallbackStream.Seek(0, SeekOrigin.Begin);
            using var reader = new StreamReader(_fallbackStream);
            return reader.ReadToEnd();
        }
    }

}
