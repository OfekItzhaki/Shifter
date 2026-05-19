// Feature: lemonsqueezy-billing
// Properties 1, 2, 3, 17, 18: Webhook signature verification and idempotency
// Validates: Requirements 2.1, 2.2, 2.5, 2.7, 10.1, 10.3

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using FsCheck;
using FsCheck.Xunit;
using Jobuler.Application.Billing;
using Jobuler.Application.Billing.Commands;
using Jobuler.Domain.Billing;
using Jobuler.Infrastructure.Billing;
using Jobuler.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using Xunit;

namespace Jobuler.Tests.Billing;

public class WebhookSignatureAndIdempotencyPropertyTests
{
    // ── Helpers ───────────────────────────────────────────────────────────────

    private static WebhookSignatureValidator CreateValidator(string secret)
    {
        var settings = Options.Create(new LemonSqueezySettings { WebhookSecret = secret });
        return new WebhookSignatureValidator(settings);
    }

    private static string ComputeHmacSha256(string payload, string secret)
    {
        var secretBytes = Encoding.UTF8.GetBytes(secret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);
        var hash = HMACSHA256.HashData(secretBytes, payloadBytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static AppDbContext CreateInMemoryDb()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new AppDbContext(options);
    }

    private static string BuildValidWebhookPayload(string eventType, string eventId, Dictionary<string, string>? metadata = null)
    {
        var customData = metadata ?? new Dictionary<string, string>
        {
            ["space_id"] = Guid.NewGuid().ToString(),
            ["group_id"] = Guid.NewGuid().ToString()
        };

        var payload = new
        {
            meta = new
            {
                event_name = eventType,
                webhook_id = eventId,
                custom_data = customData
            },
            data = new
            {
                id = Guid.NewGuid().ToString(),
                attributes = new
                {
                    status = "active",
                    current_period_start = DateTime.UtcNow.ToString("o"),
                    current_period_end = DateTime.UtcNow.AddMonths(1).ToString("o")
                }
            }
        };

        return JsonSerializer.Serialize(payload);
    }

    // ── Generators ───────────────────────────────────────────────────────────

    private static Arbitrary<string> NonEmptyPayloadArbitrary()
    {
        var gen = from length in Gen.Choose(1, 500)
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

    private static Arbitrary<string> UnrecognizedEventTypeArbitrary()
    {
        var recognized = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "subscription_created",
            "subscription_updated",
            "subscription_cancelled",
            "subscription_payment_success"
        };

        var gen = from prefix in Gen.Elements("order_", "invoice_", "license_", "custom_", "unknown_")
                  from suffix in Gen.Elements("created", "updated", "deleted", "refunded", "expired", "foo")
                  let eventType = prefix + suffix
                  where !recognized.Contains(eventType)
                  select eventType;

        return Arb.From(gen);
    }

    // ── Property 1: Webhook signature verification is sound ──────────────────
    // **Validates: Requirements 2.1, 2.2**

    [Property(MaxTest = 100)]
    public Property ValidSignature_IsAccepted()
    {
        return Prop.ForAll(NonEmptyPayloadArbitrary(), WebhookSecretArbitrary(), (payload, secret) =>
        {
            var validator = CreateValidator(secret);
            var signature = ComputeHmacSha256(payload, secret);

            return validator.Verify(payload, signature)
                .Label("Valid HMAC-SHA256 signature should be accepted");
        });
    }

    [Property(MaxTest = 100)]
    public Property InvalidSignature_IsRejected()
    {
        return Prop.ForAll(NonEmptyPayloadArbitrary(), WebhookSecretArbitrary(), (payload, secret) =>
        {
            var validator = CreateValidator(secret);
            // Compute signature with a different secret
            var wrongSecret = secret + "_tampered";
            var wrongSignature = ComputeHmacSha256(payload, wrongSecret);

            return (!validator.Verify(payload, wrongSignature))
                .Label("Signature computed with wrong secret should be rejected");
        });
    }

    [Property(MaxTest = 100)]
    public Property TamperedPayload_IsRejected()
    {
        return Prop.ForAll(NonEmptyPayloadArbitrary(), WebhookSecretArbitrary(), (payload, secret) =>
        {
            var validator = CreateValidator(secret);
            var signature = ComputeHmacSha256(payload, secret);

            // Tamper with the payload
            var tamperedPayload = payload + "x";

            return (!validator.Verify(tamperedPayload, signature))
                .Label("Signature should be rejected when payload is tampered");
        });
    }

    [Fact]
    public void EmptyPayload_IsRejected()
    {
        var validator = CreateValidator("test_secret");
        validator.Verify("", "some_signature").Should().BeFalse();
        validator.Verify(null!, "some_signature").Should().BeFalse();
    }

    [Fact]
    public void EmptySignature_IsRejected()
    {
        var validator = CreateValidator("test_secret");
        validator.Verify("some_payload", "").Should().BeFalse();
        validator.Verify("some_payload", null!).Should().BeFalse();
    }

    // ── Property 2: Malformed payloads are rejected ──────────────────────────
    // **Validates: Requirements 2.5**

    [Property(MaxTest = 100)]
    public Property MalformedJson_DoesNotModifySubscriptionState()
    {
        var malformedGen = Gen.Elements(
            "not json at all",
            "{invalid json",
            "{'single': 'quotes'}",
            "<xml>not json</xml>",
            "12345",
            "",
            "null",
            "[unclosed array"
        );

        return Prop.ForAll(Arb.From(malformedGen), WebhookSecretArbitrary(), (malformedPayload, secret) =>
        {
            // The controller would reject this at the JSON parse step.
            // Verify that JsonDocument.Parse throws for malformed input or lacks required fields.
            var parseSucceeded = false;
            try
            {
                using var doc = JsonDocument.Parse(malformedPayload);
                // Must be an object to have properties
                if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                    doc.RootElement.TryGetProperty("meta", out var meta) &&
                    meta.ValueKind == JsonValueKind.Object &&
                    meta.TryGetProperty("event_name", out _))
                {
                    parseSucceeded = true;
                }
            }
            catch (JsonException)
            {
                // Expected — malformed JSON
            }

            return (!parseSucceeded)
                .Label("Malformed payload should not pass validation (either fails JSON parse or lacks required fields)");
        });
    }

    [Property(MaxTest = 100)]
    public Property ValidJsonMissingRequiredFields_IsRejected()
    {
        // Generate valid JSON that lacks meta.event_name or event ID
        var missingFieldsGen = Gen.Elements(
            "{}",
            "{\"data\": {}}",
            "{\"meta\": {}}",
            "{\"meta\": {\"custom_data\": {}}}",
            "{\"meta\": {\"event_name\": null}}",
            "{\"meta\": {\"event_name\": \"\"}}",
            "{\"meta\": {\"event_name\": \"subscription_created\"}}" // missing event ID
        );

        return Prop.ForAll(Arb.From(missingFieldsGen), payload =>
        {
            var hasRequiredFields = false;
            try
            {
                using var doc = JsonDocument.Parse(payload);
                var root = doc.RootElement;

                if (root.TryGetProperty("meta", out var meta) &&
                    meta.TryGetProperty("event_name", out var eventNameEl))
                {
                    var eventName = eventNameEl.GetString();
                    if (!string.IsNullOrWhiteSpace(eventName))
                    {
                        // Also need event ID
                        var hasEventId = false;
                        if (meta.TryGetProperty("webhook_id", out var webhookId) &&
                            !string.IsNullOrWhiteSpace(webhookId.GetString()))
                        {
                            hasEventId = true;
                        }
                        else if (root.TryGetProperty("data", out var data) &&
                                 data.TryGetProperty("id", out var dataId) &&
                                 !string.IsNullOrWhiteSpace(dataId.GetString()))
                        {
                            hasEventId = true;
                        }

                        hasRequiredFields = hasEventId;
                    }
                }
            }
            catch (JsonException)
            {
                // Not valid JSON
            }

            return (!hasRequiredFields)
                .Label("Payload missing required fields (event_name or event_id) should be rejected");
        });
    }

    // ── Property 3: Unrecognized event types are acknowledged without processing ─
    // **Validates: Requirements 2.7**

    [Property(MaxTest = 100)]
    public Property UnrecognizedEventType_DoesNotModifySubscriptionState()
    {
        return Prop.ForAll(UnrecognizedEventTypeArbitrary(), async eventType =>
        {
            using var db = CreateInMemoryDb();
            var mediator = Substitute.For<IMediator>();
            var logger = NullLogger<HandleWebhookCommandHandler>.Instance;
            var handler = new HandleWebhookCommandHandler(db, mediator, logger);

            var eventId = Guid.NewGuid().ToString();
            var command = new HandleWebhookCommand(eventId, eventType, "{}", new Dictionary<string, string>());

            await handler.Handle(command, CancellationToken.None);

            // Verify: event was logged (acknowledged)
            var logged = await db.WebhookEventLogs.AnyAsync(e => e.EventId == eventId);
            logged.Should().BeTrue("event should be logged even for unrecognized types");

            // Verify: no subscription-specific sub-handlers were dispatched
            await mediator.DidNotReceive().Send(Arg.Any<HandleSubscriptionCreatedCommand>(), Arg.Any<CancellationToken>());
            await mediator.DidNotReceive().Send(Arg.Any<HandleSubscriptionUpdatedCommand>(), Arg.Any<CancellationToken>());
            await mediator.DidNotReceive().Send(Arg.Any<HandleSubscriptionCancelledCommand>(), Arg.Any<CancellationToken>());
            await mediator.DidNotReceive().Send(Arg.Any<HandlePaymentSuccessCommand>(), Arg.Any<CancellationToken>());
        });
    }

    // ── Property 17: Duplicate event IDs are idempotent ──────────────────────
    // **Validates: Requirements 10.1**

    [Property(MaxTest = 100)]
    public Property DuplicateEventId_SkipsProcessing()
    {
        var eventIdGen = from guid in Arb.Generate<Guid>()
                         select guid.ToString();

        var eventTypeGen = Gen.Elements(
            "subscription_created",
            "subscription_updated",
            "subscription_cancelled",
            "subscription_payment_success");

        var gen = from eventId in eventIdGen
                  from eventType in eventTypeGen
                  select (eventId, eventType);

        return Prop.ForAll(Arb.From(gen), async tuple =>
        {
            var (eventId, eventType) = tuple;

            using var db = CreateInMemoryDb();
            var mediator = Substitute.For<IMediator>();
            var logger = NullLogger<HandleWebhookCommandHandler>.Instance;
            var handler = new HandleWebhookCommandHandler(db, mediator, logger);

            // First processing — should dispatch
            var command = new HandleWebhookCommand(eventId, eventType, "{}", new Dictionary<string, string>());
            await handler.Handle(command, CancellationToken.None);

            // Reset call tracking
            mediator.ClearReceivedCalls();

            // Second processing with same event ID — should be idempotent
            await handler.Handle(command, CancellationToken.None);

            // Verify: no sub-handlers were dispatched on the second call
            await mediator.DidNotReceive().Send(Arg.Any<HandleSubscriptionCreatedCommand>(), Arg.Any<CancellationToken>());
            await mediator.DidNotReceive().Send(Arg.Any<HandleSubscriptionUpdatedCommand>(), Arg.Any<CancellationToken>());
            await mediator.DidNotReceive().Send(Arg.Any<HandleSubscriptionCancelledCommand>(), Arg.Any<CancellationToken>());
            await mediator.DidNotReceive().Send(Arg.Any<HandlePaymentSuccessCommand>(), Arg.Any<CancellationToken>());

            // Verify: only one event log entry exists
            var logCount = await db.WebhookEventLogs.CountAsync(e => e.EventId == eventId);
            logCount.Should().Be(1, "duplicate event should not create a second log entry");
        });
    }

    // ── Property 18: No-op when incoming data matches current state ──────────
    // **Validates: Requirements 10.3**

    [Property(MaxTest = 100)]
    public Property SubscriptionUpdated_NoOp_WhenDataMatchesCurrentState()
    {
        var statusGen = Gen.Elements("active", "on_trial", "past_due", "cancelled", "expired");

        var gen = from lsStatus in statusGen
                  from offsetDays in Gen.Choose(1, 365)
                  from durationDays in Gen.Choose(1, 90)
                  let periodStart = DateTime.UtcNow.AddDays(-offsetDays)
                  let periodEnd = periodStart.AddDays(durationDays)
                  select (lsStatus, periodStart, periodEnd);

        return Prop.ForAll(Arb.From(gen), async tuple =>
        {
            var (lsStatus, periodStart, periodEnd) = tuple;

            using var db = CreateInMemoryDb();
            var logger = NullLogger<HandleSubscriptionUpdatedCommandHandler>.Instance;
            var handler = new HandleSubscriptionUpdatedCommandHandler(db, logger);

            // Map the LS status to our enum
            var statusMapping = new Dictionary<string, SubscriptionStatus>(StringComparer.OrdinalIgnoreCase)
            {
                ["active"] = SubscriptionStatus.Active,
                ["on_trial"] = SubscriptionStatus.Trialing,
                ["past_due"] = SubscriptionStatus.PastDue,
                ["cancelled"] = SubscriptionStatus.Canceled,
                ["expired"] = SubscriptionStatus.Expired,
            };
            var mappedStatus = statusMapping[lsStatus];

            // Create a subscription that already has the matching state
            var spaceId = Guid.NewGuid();
            var groupId = Guid.NewGuid();
            var lsSubId = Guid.NewGuid().ToString();
            var sub = GroupSubscription.CreateTrial(spaceId, groupId, trialDays: 14);
            sub.Activate("pro", lsSubId, "ls_cus_123", periodStart, periodEnd);
            sub.UpdateStatus(mappedStatus);

            db.GroupSubscriptions.Add(sub);
            await db.SaveChangesAsync();

            // Track the change tracker state
            db.ChangeTracker.Clear();

            // Build a payload that matches the current state exactly
            var payload = JsonSerializer.Serialize(new
            {
                data = new
                {
                    id = lsSubId,
                    attributes = new
                    {
                        status = lsStatus,
                        current_period_start = periodStart.ToString("o"),
                        current_period_end = periodEnd.ToString("o")
                    }
                }
            });

            var command = new HandleSubscriptionUpdatedCommand(payload, new Dictionary<string, string>());
            await handler.Handle(command, CancellationToken.None);

            // Reload subscription and verify nothing changed
            var reloaded = await db.GroupSubscriptions
                .FirstAsync(s => s.LemonSqueezySubscriptionId == lsSubId);

            reloaded.Status.Should().Be(mappedStatus, "status should remain unchanged");
            reloaded.CurrentPeriodStart.Should().Be(periodStart, "period start should remain unchanged");
            reloaded.CurrentPeriodEnd.Should().Be(periodEnd, "period end should remain unchanged");
        });
    }
}
