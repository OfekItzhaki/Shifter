// Feature: space-billing, Property 17: Webhook signature rejection
// **Validates: Requirements 5.4**
// For any webhook request where the HMAC signature does not match the computed
// signature of the payload, the controller SHALL return 401 Unauthorized and
// SHALL NOT dispatch any command or modify any database state.

using System.Text;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Api.Controllers;
using Jobuler.Application.Billing;
using Jobuler.Application.Billing.Commands;
using Jobuler.Domain.Billing;
using Jobuler.Infrastructure.Billing;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Billing;

public class WebhookSignatureRejectionPropertyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    /// <summary>
    /// Creates a controller with a real WebhookSignatureValidator (using the given secret)
    /// and a mock mediator, then sets up the HTTP context with the given payload and signature header.
    /// </summary>
    private static (LemonSqueezyWebhookController Controller, IMediator Mediator) CreateControllerWithSignature(
        string payload, string signatureHeader, string webhookSecret)
    {
        var settings = Options.Create(new LemonSqueezySettings { WebhookSecret = webhookSecret });
        var validator = new WebhookSignatureValidator(settings);
        var mediator = Substitute.For<IMediator>();
        var logger = NullLogger<LemonSqueezyWebhookController>.Instance;

        var controller = new LemonSqueezyWebhookController(mediator, validator, logger);

        // Set up HttpContext with the payload as the request body
        var httpContext = new DefaultHttpContext();
        httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
        httpContext.Request.Headers["X-Signature"] = signatureHeader;

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        return (controller, mediator);
    }

    // ── Generators ───────────────────────────────────────────────────────────

    private static Arbitrary<string> NonEmptyPayloadArbitrary()
    {
        var gen = from length in Gen.Choose(10, 500)
                  from chars in Gen.ArrayOf(length, Gen.Elements(
                      "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789{}:,\"[] ".ToCharArray()))
                  select new string(chars);

        return Arb.From(gen);
    }

    private static Arbitrary<string> WebhookSecretArbitrary()
    {
        var gen = from length in Gen.Choose(16, 64)
                  from chars in Gen.ArrayOf(length, Gen.Elements(
                      "abcdefghijklmnopqrstuvwxyz0123456789".ToCharArray()))
                  select new string(chars);

        return Arb.From(gen);
    }

    private static Arbitrary<string> InvalidSignatureArbitrary()
    {
        // Generate random hex strings that won't match a valid HMAC
        var gen = Gen.OneOf(
            // Random hex string (wrong length or wrong content)
            from length in Gen.Choose(1, 128)
            from chars in Gen.ArrayOf(length, Gen.Elements("0123456789abcdef".ToCharArray()))
            select new string(chars),
            // Completely random string (not even hex)
            from length in Gen.Choose(1, 100)
            from chars in Gen.ArrayOf(length, Gen.Elements(
                "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789!@#$%^&*".ToCharArray()))
            select new string(chars),
            // Empty string
            Gen.Constant("")
        );

        return Arb.From(gen);
    }

    // ── Property 17: Invalid HMAC returns 401, no state modification ─────────

    [Property(MaxTest = 100)]
    public Property InvalidSignature_Returns401_AndDoesNotDispatchCommands()
    {
        return Prop.ForAll(
            NonEmptyPayloadArbitrary(),
            WebhookSecretArbitrary(),
            InvalidSignatureArbitrary(),
            (payload, secret, invalidSignature) =>
            {
                var (controller, mediator) = CreateControllerWithSignature(payload, invalidSignature, secret);

                var result = controller.HandleWebhook(CancellationToken.None).GetAwaiter().GetResult();

                // Verify: returns 401 Unauthorized
                var is401 = result is UnauthorizedResult;

                // Verify: mediator was never called (no command dispatched)
                mediator.DidNotReceive().Send(
                    Arg.Any<IRequest>(), Arg.Any<CancellationToken>());

                return is401.Label("Should return 401 Unauthorized for invalid signature")
                    .And(true.Label("Mediator should not be called"));
            });
    }

    [Property(MaxTest = 100)]
    public Property WrongSecret_Returns401_AndDoesNotDispatchCommands()
    {
        // Use a valid-looking payload but sign with a different secret
        var gen = from secret in WebhookSecretArbitrary().Generator
                  from wrongSecret in WebhookSecretArbitrary().Generator
                  where secret != wrongSecret
                  from payload in NonEmptyPayloadArbitrary().Generator
                  let wrongSignature = ComputeHmacSha256(payload, wrongSecret)
                  select (payload, secret, wrongSignature);

        return Prop.ForAll(Arb.From(gen), tuple =>
        {
            var (payload, correctSecret, wrongSignature) = tuple;

            var (controller, mediator) = CreateControllerWithSignature(payload, wrongSignature, correctSecret);

            var result = controller.HandleWebhook(CancellationToken.None).GetAwaiter().GetResult();

            // Verify: returns 401 Unauthorized
            var is401 = result is UnauthorizedResult;

            // Verify: mediator was never called
            mediator.DidNotReceive().Send(
                Arg.Any<IRequest>(), Arg.Any<CancellationToken>());

            return is401.Label("Should return 401 when signature computed with wrong secret")
                .And(true.Label("Mediator should not be called"));
        });
    }

    [Property(MaxTest = 100)]
    public Property InvalidSignature_DoesNotCreateWebhookEventLog()
    {
        return Prop.ForAll(
            WebhookSecretArbitrary(),
            InvalidSignatureArbitrary(),
            (secret, invalidSignature) =>
            {
                // Use a valid JSON payload that would normally be processed
                var validPayload = BuildValidWebhookPayload();

                using var db = CreateInMemoryDb();
                var settings = Options.Create(new LemonSqueezySettings { WebhookSecret = secret });
                var validator = new WebhookSignatureValidator(settings);
                var mediator = Substitute.For<IMediator>();
                var logger = NullLogger<LemonSqueezyWebhookController>.Instance;

                var controller = new LemonSqueezyWebhookController(mediator, validator, logger);

                var httpContext = new DefaultHttpContext();
                httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(validPayload));
                httpContext.Request.Headers["X-Signature"] = invalidSignature;
                controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

                var result = controller.HandleWebhook(CancellationToken.None).GetAwaiter().GetResult();

                // Verify: 401 returned
                var is401 = result is UnauthorizedResult;

                // Verify: no WebhookEventLog was created (mediator never called = no DB write)
                mediator.DidNotReceive().Send(
                    Arg.Any<HandleWebhookCommand>(), Arg.Any<CancellationToken>());

                return is401.Label("Should return 401 for invalid signature on valid payload");
            });
    }

    [Property(MaxTest = 100)]
    public Property InvalidSignature_DoesNotModifySpaceSubscription()
    {
        var gen = from secret in WebhookSecretArbitrary().Generator
                  from invalidSig in InvalidSignatureArbitrary().Generator
                  select (secret, invalidSig);

        return Prop.ForAll(Arb.From(gen), async tuple =>
        {
            var (secret, invalidSignature) = tuple;

            using var db = CreateInMemoryDb();

            // Pre-seed a SpaceSubscription in the database
            var spaceId = Guid.NewGuid();
            var subscription = SpaceSubscription.CreateTrial(spaceId, 14);
            db.SpaceSubscriptions.Add(subscription);
            await db.SaveChangesAsync();

            // Capture original state
            var originalStatus = subscription.Status;
            var originalTrialEndsAt = subscription.TrialEndsAt;
            var originalTierId = subscription.TierId;

            db.ChangeTracker.Clear();

            // Build a payload that references this space and would normally activate it
            var payload = BuildActivationPayload(spaceId);

            var settings = Options.Create(new LemonSqueezySettings { WebhookSecret = secret });
            var validator = new WebhookSignatureValidator(settings);
            var mediator = Substitute.For<IMediator>();
            var logger = NullLogger<LemonSqueezyWebhookController>.Instance;

            var controller = new LemonSqueezyWebhookController(mediator, validator, logger);

            var httpContext = new DefaultHttpContext();
            httpContext.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(payload));
            httpContext.Request.Headers["X-Signature"] = invalidSignature;
            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

            var result = controller.HandleWebhook(CancellationToken.None);
            var actionResult = await result;

            // Verify: 401 returned
            actionResult.Should().BeOfType<UnauthorizedResult>();

            // Verify: SpaceSubscription was NOT modified
            var reloaded = await db.SpaceSubscriptions.FirstAsync(s => s.SpaceId == spaceId);
            reloaded.Status.Should().Be(originalStatus, "subscription status should not change on invalid signature");
            reloaded.TrialEndsAt.Should().Be(originalTrialEndsAt, "trial end date should not change on invalid signature");
            reloaded.TierId.Should().Be(originalTierId, "tier should not change on invalid signature");

            // Verify: no WebhookEventLog was created
            var eventLogCount = await db.WebhookEventLogs.CountAsync();
            eventLogCount.Should().Be(0, "no webhook event log should be created when signature is invalid");
        });
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private static string ComputeHmacSha256(string payload, string secret)
    {
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = System.Security.Cryptography.HMACSHA256.HashData(secretBytes, payloadBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string BuildValidWebhookPayload()
    {
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            meta = new
            {
                event_name = "subscription_created",
                webhook_id = Guid.NewGuid().ToString(),
                custom_data = new { space_id = Guid.NewGuid().ToString() }
            },
            data = new
            {
                id = Guid.NewGuid().ToString(),
                attributes = new
                {
                    status = "active",
                    variant_id = 12345,
                    current_period_start = DateTime.UtcNow.ToString("o"),
                    current_period_end = DateTime.UtcNow.AddMonths(1).ToString("o")
                }
            }
        });
    }

    private static string BuildActivationPayload(Guid spaceId)
    {
        return System.Text.Json.JsonSerializer.Serialize(new
        {
            meta = new
            {
                event_name = "subscription_created",
                webhook_id = Guid.NewGuid().ToString(),
                custom_data = new { space_id = spaceId.ToString() }
            },
            data = new
            {
                id = Guid.NewGuid().ToString(),
                attributes = new
                {
                    status = "active",
                    variant_id = 99999,
                    current_period_start = DateTime.UtcNow.ToString("o"),
                    current_period_end = DateTime.UtcNow.AddMonths(1).ToString("o")
                }
            }
        });
    }
}
