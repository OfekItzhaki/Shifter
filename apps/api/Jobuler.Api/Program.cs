using Jobuler.Application.AI.Import;
using FluentValidation;
using Jobuler.Api.Middleware;
using Jobuler.Application.Billing;
using Jobuler.Application.Exports;
using Jobuler.Application.Feedback;
using Jobuler.Application.HomeLeave;
using Jobuler.Application.Notifications;
using Jobuler.Infrastructure.Billing;
using Jobuler.Infrastructure.Exports;
using Jobuler.Infrastructure.Notifications;
using Jobuler.Application.AI;
using Jobuler.Application.Auth.Commands;
using Jobuler.Application.Common;
using Jobuler.Application.Scheduling;
using Jobuler.Application.Scheduling.SelfService;
using Jobuler.Infrastructure.AI;
using Jobuler.Infrastructure.Auth;
using Jobuler.Infrastructure.Caching;
using Jobuler.Infrastructure.Email;
using Jobuler.Application.Auth;
using Jobuler.Infrastructure.Logging;
using Jobuler.Infrastructure.Persistence;
using Jobuler.Infrastructure.Scheduling;
using Jobuler.Infrastructure.Security;
using Jobuler.Infrastructure.Storage;
using Jobuler.Infrastructure.Timezone;
using MediatR;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;
using StackExchange.Redis;
using System.Text;
using System.Threading.RateLimiting;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// ─── Serilog ─────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .CreateLogger();

builder.Host.UseSerilog();

var fieldProtectionSecret = FirstConfigured(
        builder.Configuration["DataProtection:FieldEncryptionKey"],
        Environment.GetEnvironmentVariable("FIELD_ENCRYPTION_KEY"),
        builder.Configuration["Jwt:Secret"])
    ?? throw new InvalidOperationException("DataProtection:FieldEncryptionKey or Jwt:Secret must be configured.");

if (string.IsNullOrWhiteSpace(builder.Configuration["DataProtection:FieldEncryptionKey"])
    && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("FIELD_ENCRYPTION_KEY")))
{
    Log.Warning("DataProtection:FieldEncryptionKey is not configured. Falling back to Jwt:Secret for local field protection; configure a dedicated secret before production use.");
}

FieldEncryption.Configure(fieldProtectionSecret);

// ─── Database ────────────────────────────────────────────────────────────────
// Tell AppDbContext (defined in Application) where to find EF configurations
AppDbContext.ConfigurationAssembly = typeof(Jobuler.Infrastructure.Persistence.Configurations.NotificationConfiguration).Assembly;

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// ─── Auth ────────────────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret not configured");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = builder.Configuration["Jwt:Audience"],
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization();

static string? FirstConfigured(params string?[] values) =>
    values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

// ─── Application services ────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(LoginCommand).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

builder.Services.AddValidatorsFromAssembly(typeof(LoginCommand).Assembly);

builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddSingleton<IContactLookupProtector, ContactLookupProtector>();
builder.Services.AddHostedService<ContactFieldProtectionBackfillService>();

// ─── WebAuthn / FIDO2 ────────────────────────────────────────────────────────
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<Fido2NetLib.IFido2>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var fido2Config = new Fido2NetLib.Fido2Configuration
    {
        ServerDomain = config["WebAuthn:RelyingPartyId"] ?? "localhost",
        ServerName = config["WebAuthn:RelyingPartyName"] ?? "Shifter",
        Origins = new HashSet<string> { config["WebAuthn:Origin"] ?? "https://localhost" }
    };
    return new Fido2NetLib.Fido2(fido2Config);
});
builder.Services.AddScoped<IWebAuthnService, Fido2Service>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();
builder.Services.AddScoped<ISystemLogger, SystemLogger>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IPushNotificationSender, PushNotificationSender>();
builder.Services.AddScoped<IPdfRenderer, QuestPdfRenderer>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ITimezoneResolver, TimezoneResolver>();

// ─── VAPID configuration (Web Push) ──────────────────────────────────────────
builder.Services.Configure<VapidSettings>(options =>
{
    options.PublicKey = builder.Configuration["VAPID_PUBLIC_KEY"]
        ?? Environment.GetEnvironmentVariable("VAPID_PUBLIC_KEY")
        ?? string.Empty;
    options.PrivateKey = builder.Configuration["VAPID_PRIVATE_KEY"]
        ?? Environment.GetEnvironmentVariable("VAPID_PRIVATE_KEY")
        ?? string.Empty;
    options.Subject = builder.Configuration["VAPID_SUBJECT"]
        ?? Environment.GetEnvironmentVariable("VAPID_SUBJECT")
        ?? string.Empty;
});

// Named HttpClient for Web Push service requests (used by PushNotificationSender)
builder.Services.AddHttpClient("WebPush", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
});

// ─── Feedback options ─────────────────────────────────────────────────────────
builder.Services.Configure<FeedbackOptions>(builder.Configuration.GetSection("Feedback"));

// ─── LemonSqueezy billing ─────────────────────────────────────────────────────
builder.Services.Configure<LemonSqueezySettings>(builder.Configuration.GetSection("LemonSqueezy"));
builder.Services.Configure<BillingOptions>(builder.Configuration.GetSection("LemonSqueezy"));

// Validate settings at startup — warn if required values are missing (billing features will be unavailable)
var lemonSqueezySettings = builder.Configuration.GetSection("LemonSqueezy").Get<LemonSqueezySettings>();
if (lemonSqueezySettings is not null)
{
    try
    {
        lemonSqueezySettings.Validate();
    }
    catch (InvalidOperationException ex)
    {
        // Log warning but don't crash — billing endpoints will fail at runtime instead
        Console.WriteLine($"⚠️  LemonSqueezy configuration incomplete: {ex.Message}");
    }
}
else
{
    Console.WriteLine("⚠️  LemonSqueezy configuration section is missing. Billing features will be unavailable.");
}

builder.Services.AddHttpClient<ILemonSqueezyClient, LemonSqueezyClient>(client =>
{
    client.BaseAddress = new Uri("https://api.lemonsqueezy.com/");
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services.AddSingleton<IWebhookSignatureValidator, WebhookSignatureValidator>();
builder.Services.AddScoped<IStatisticsPeriodService, StatisticsPeriodService>();
builder.Services.AddScoped<IPeakMemberTracker, PeakMemberTracker>();

builder.Services.AddHttpClient("TrialDurationCache", (sp, client) =>
{
    var lsSettings = sp.GetRequiredService<IOptions<LemonSqueezySettings>>().Value;
    client.BaseAddress = new Uri("https://api.lemonsqueezy.com/");
    client.Timeout = TimeSpan.FromSeconds(10);
    client.DefaultRequestHeaders.Authorization =
        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", lsSettings.ApiKey);
    client.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
});
builder.Services.AddSingleton<ITrialDurationCache, TrialDurationCache>();

// ─── Email: SendGrid (real) or NoOp (dev fallback) ────────────────────────────
if (!string.IsNullOrWhiteSpace(builder.Configuration["SendGrid:ApiKey"]))
{
    builder.Services.AddScoped<IEmailSender, SendGridEmailSender>();
    Log.Information("Email provider: SendGrid");
}
else
{
    builder.Services.AddScoped<IEmailSender, NoOpEmailSender>();
    Log.Warning("SendGrid:ApiKey not configured — emails will be logged only (NoOp)");
}

// ─── WhatsApp: Twilio (always registered — graceful no-op if unconfigured) ───
builder.Services.AddScoped<TwilioWhatsAppSender>();

// ─── Notification routing: phone → WhatsApp, email → SendGrid ────────────────
if (!string.IsNullOrWhiteSpace(builder.Configuration["Twilio:AccountSid"]) ||
    !string.IsNullOrWhiteSpace(builder.Configuration["SendGrid:ApiKey"]))
{
    builder.Services.AddScoped<INotificationSender, RoutingNotificationSender>();
    Log.Information("Notification sender: RoutingNotificationSender (Twilio + SendGrid)");
}
else
{
    builder.Services.AddScoped<INotificationSender, NoOpNotificationSender>();
    Log.Warning("Twilio and SendGrid not configured — notifications will be logged only (NoOp)");
}

// ─── Invitation senders ───────────────────────────────────────────────────────
builder.Services.AddScoped<NoOpInvitationSender>();
builder.Services.AddScoped<EmailInvitationSender>();
builder.Services.AddScoped<WhatsAppInvitationSender>();
builder.Services.AddScoped<IInvitationSender, CompositeInvitationSender>();

// ─── Schedule notifications ───────────────────────────────────────────────────
builder.Services.AddScoped<IScheduleNotificationSender, ScheduleNotificationSender>();

// ─── Recall notifications ─────────────────────────────────────────────────────
builder.Services.AddScoped<Jobuler.Application.HomeLeave.Services.IRecallNotificationService, RecallNotificationService>();
// ─── File storage ─────────────────────────────────────────────────────────────
// Use S3-compatible storage when Storage:S3:BucketName is configured, otherwise local disk.
if (!string.IsNullOrWhiteSpace(builder.Configuration["Storage:S3:BucketName"]))
{
    builder.Services.AddScoped<IFileStorage, Jobuler.Infrastructure.Storage.S3FileStorage>();
    Log.Information("File storage: S3-compatible (bucket={Bucket})", builder.Configuration["Storage:S3:BucketName"]);
}
else
{
    builder.Services.AddScoped<IFileStorage, LocalDiskFileStorage>();
    Log.Information("File storage: local disk (wwwroot/uploads)");
}

// ─── Redis ───────────────────────────────────────────────────────────────────
var redisConn = builder.Configuration["Redis:ConnectionString"] ?? "localhost:6379";
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
{
    var config = ConfigurationOptions.Parse(redisConn);
    config.AbortOnConnectFail = false;   // don't crash if Redis is temporarily down
    config.ConnectTimeout = 5000;
    config.ReconnectRetryPolicy = new ExponentialRetry(500);
    return ConnectionMultiplexer.Connect(config);
});

// ─── Caching ─────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<Jobuler.Application.Common.ICacheService, Jobuler.Infrastructure.Caching.RedisCacheService>();

// ─── Home-leave services ─────────────────────────────────────────────────────
builder.Services.AddSingleton<IOptimalRatioCalculator, OptimalRatioCalculator>();
builder.Services.AddSingleton<IFeasibilityEngine, FeasibilityEngine>();

// ─── Scheduling services ─────────────────────────────────────────────────────
builder.Services.AddScoped<IAssignmentSnapshotService, AssignmentSnapshotService>();
builder.Services.AddScoped<ICumulativeTracker, CumulativeTracker>();
builder.Services.AddScoped<IPeriodManager, PeriodManager>();
builder.Services.AddScoped<ISolverPayloadNormalizer, SolverPayloadNormalizer>();
builder.Services.AddScoped<IRecommendationEngine, RecommendationEngine>();

// ─── Self-service scheduling services ─────────────────────────────────────────
builder.Services.AddScoped<ISlotGenerationService, SlotGenerationService>();
builder.Services.AddScoped<IShiftRequestService, ShiftRequestService>();
builder.Services.AddScoped<ISlotAvailabilityEngine, SlotAvailabilityEngine>();
builder.Services.AddScoped<IWaitlistService, WaitlistService>();
builder.Services.AddScoped<IShiftSwapService, ShiftSwapService>();
builder.Services.AddScoped<ISlotLockService, PostgresAdvisoryLockService>();

// ─── Conflict detection ──────────────────────────────────────────────────────
// ConflictDetectionDbContext uses the same connection string but WITHOUT the RLS
// session variable interceptor — it needs cross-space read access for LinkedUserId resolution.
builder.Services.AddDbContext<ConflictDetectionDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<Jobuler.Application.Conflicts.IConflictDetectionService,
    Jobuler.Infrastructure.Conflicts.ConflictDetectionService>();

// Use Redis queue if available, fall back to in-memory queue (no Redis required)
builder.Services.AddSingleton<ISolverJobQueue>(sp =>
{
    var logger = sp.GetRequiredService<ILogger<InMemorySolverJobQueue>>();
    try
    {
        var redis = sp.GetRequiredService<IConnectionMultiplexer>();
        var db = redis.GetDatabase();
        db.Ping();
        sp.GetRequiredService<ILogger<Program>>()
            .LogInformation("Redis available — using Redis solver queue.");
        return new RedisSolverJobQueue(redis, sp.GetRequiredService<ILogger<RedisSolverJobQueue>>());
    }
    catch
    {
        sp.GetRequiredService<ILogger<Program>>()
            .LogInformation("Redis unavailable — using in-memory solver queue (single-server mode).");
        return new InMemorySolverJobQueue(logger);
    }
});

builder.Services.AddHttpClient<ISolverClient, SolverHttpClient>(client =>
{
    var solverUrl = builder.Configuration["Solver:BaseUrl"] ?? "http://localhost:8000";
    client.BaseAddress = new Uri(solverUrl);
    // Solver can run up to 60s (CP-SAT) + constraint build time.
    // Progress is shown to the user via polling, so longer runs are acceptable.
    client.Timeout = TimeSpan.FromSeconds(90);
});

// ─── AI assistant (optional — only registered when API key is configured) ────
builder.Services.AddSingleton<IStructuredImportParser, StructuredImportParser>();

if (!string.IsNullOrEmpty(builder.Configuration["AI:ApiKey"]))
{
    builder.Services.AddHttpClient<IAiAssistant, OpenAiAssistant>();
}
else
{
    // Register a no-op fallback so the app starts without AI configured
    builder.Services.AddSingleton<IAiAssistant, NoOpAiAssistant>();
}

// Background worker — dequeues and processes solver jobs
builder.Services.AddHostedService<SolverWorkerService>();

// Auto-scheduler — triggers solver automatically when schedule coverage is insufficient
builder.Services.AddHostedService<AutoSchedulerService>();

// Subscription cleanup — auto-deletes groups 6 months after subscription cancellation
builder.Services.AddHostedService<SubscriptionCleanupService>();

// Subscription expiry — transitions canceled subscriptions past their billing period to Expired
builder.Services.AddHostedService<ExpireSubscriptionsJob>();

// Self-service slot generation — generates shift slots for upcoming cycles daily at midnight UTC
builder.Services.AddHostedService<GenerateCycleSlotsJob>();

// Waitlist offer expiry — expires timed-out waitlist offers and cascades to next member
builder.Services.AddHostedService<ProcessExpiredWaitlistOffersJob>();

// Request window open notifications — notifies group members when request windows open
builder.Services.AddHostedService<NotifyRequestWindowOpenJob>();

// Under-scheduled member detection — checks for members below Min_Shifts when request windows close
builder.Services.AddHostedService<CheckUnderScheduledMembersJob>();

// Swap request expiry — marks pending swap requests older than 72h as expired
builder.Services.AddHostedService<ExpireSwapRequestsJob>();

// ─── Health check monitoring & alerting ──────────────────────────────────────
builder.Services.Configure<Jobuler.Application.Common.HealthChecks.HealthCheckOptions>(options =>
{
    // Bind from HealthCheck configuration section first (appsettings.json)
    var section = builder.Configuration.GetSection("HealthCheck");
    if (section.Exists())
    {
        section.Bind(options);
    }

    // Environment variables override configuration section values
    var pushoverUserKey = builder.Configuration["PUSHOVER_USER_KEY"]
        ?? Environment.GetEnvironmentVariable("PUSHOVER_USER_KEY");
    if (!string.IsNullOrEmpty(pushoverUserKey))
        options.PushoverUserKey = pushoverUserKey;

    var pushoverAppToken = builder.Configuration["PUSHOVER_APP_TOKEN"]
        ?? Environment.GetEnvironmentVariable("PUSHOVER_APP_TOKEN");
    if (!string.IsNullOrEmpty(pushoverAppToken))
        options.PushoverAppToken = pushoverAppToken;

    var intervalStr = builder.Configuration["HEALTH_CHECK_INTERVAL_SECONDS"]
        ?? Environment.GetEnvironmentVariable("HEALTH_CHECK_INTERVAL_SECONDS");
    if (int.TryParse(intervalStr, out var interval))
        options.IntervalSeconds = interval;

    var cooldownStr = builder.Configuration["HEALTH_CHECK_ALERT_COOLDOWN_SECONDS"]
        ?? Environment.GetEnvironmentVariable("HEALTH_CHECK_ALERT_COOLDOWN_SECONDS");
    if (int.TryParse(cooldownStr, out var cooldown))
        options.AlertCooldownSeconds = cooldown;
});

// Individual service health checks
builder.Services.AddScoped<Jobuler.Application.Common.HealthChecks.IServiceHealthCheck, Jobuler.Infrastructure.HealthChecks.PostgresHealthCheck>();
builder.Services.AddScoped<Jobuler.Application.Common.HealthChecks.IServiceHealthCheck, Jobuler.Infrastructure.HealthChecks.RedisHealthCheck>();
builder.Services.AddScoped<Jobuler.Application.Common.HealthChecks.IServiceHealthCheck, Jobuler.Infrastructure.HealthChecks.LemonSqueezyHealthCheck>();
builder.Services.AddScoped<Jobuler.Application.Common.HealthChecks.IServiceHealthCheck, Jobuler.Infrastructure.HealthChecks.SendGridHealthCheck>();
builder.Services.AddScoped<Jobuler.Application.Common.HealthChecks.IServiceHealthCheck, Jobuler.Infrastructure.HealthChecks.SolverHealthCheck>();

// Health check runner (aggregates all IServiceHealthCheck instances)
builder.Services.AddScoped<Jobuler.Application.Common.HealthChecks.IHealthCheckRunner, Jobuler.Infrastructure.HealthChecks.HealthCheckRunner>();

// Pushover notifier
builder.Services.AddSingleton<Jobuler.Application.Common.HealthChecks.IPushoverNotifier, Jobuler.Infrastructure.HealthChecks.PushoverNotifier>();

// Named HttpClient instances for health checks
builder.Services.AddHttpClient("Pushover");
builder.Services.AddHttpClient("LemonSqueezy");
builder.Services.AddHttpClient("SendGrid");
builder.Services.AddHttpClient("Solver", client =>
{
    var solverUrl = builder.Configuration["Solver:BaseUrl"]
        ?? Environment.GetEnvironmentVariable("SOLVER_URL")
        ?? "http://localhost:8000";
    client.BaseAddress = new Uri(solverUrl);
});

// Background health monitor service
builder.Services.AddHostedService<Jobuler.Infrastructure.HealthChecks.HealthCheckMonitorService>();

// ─── API ─────────────────────────────────────────────────────────────────────
builder.Services.AddControllers()
    .AddJsonOptions(o =>
        o.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "Jobuler API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Enter your JWT access token"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

// ─── Rate limiting ───────────────────────────────────────────────────────────
builder.Services.AddRateLimiter(options =>
{
    // Global limit per IP — 2000 requests per minute (generous for SPA + dev testing)
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 2000,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 50
            }));

    // Strict limit on auth endpoints — prevents brute force but allows normal SPA usage
    options.AddFixedWindowLimiter("auth", o =>
    {
        o.PermitLimit = builder.Environment.IsDevelopment() ? 200 : 60;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit = 0;
    });

    // General API limit per IP
    options.AddFixedWindowLimiter("api", o =>
    {
        o.PermitLimit = 200;
        o.Window = TimeSpan.FromMinutes(1);
        o.QueueProcessingOrder = QueueProcessingOrder.OldestFirst;
        o.QueueLimit = 5;
    });

    options.RejectionStatusCode = 429;
});
var frontendUrl = builder.Configuration["App:FrontendBaseUrl"]?.TrimEnd('/') ?? "https://shifter.ofeklabs.com";
var corsOrigins = builder.Environment.IsDevelopment()
    ? new[] { "http://localhost:3000", frontendUrl }
    : new[] { frontendUrl };
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins(corsOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials()));

var app = builder.Build();

// ─── Middleware pipeline ──────────────────────────────────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();

// ─── Security headers ─────────────────────────────────────────────────────────
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("Referrer-Policy", "no-referrer");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Permissions-Policy", "camera=(), microphone=(), geolocation=()");
    context.Response.Headers.Remove("Server");
    context.Response.Headers.Remove("X-Powered-By");
    if (!app.Environment.IsDevelopment())
    {
        context.Response.Headers.Append("Strict-Transport-Security", "max-age=31536000; includeSubDomains");
    }
    await next();
});
app.UseSerilogRequestLogging();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseStaticFiles(); // serves wwwroot/uploads/* at /uploads/*
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantContextMiddleware>();
app.MapControllers();

app.Run();
