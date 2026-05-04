using FluentValidation;
using Jobuler.Api.Middleware;
using Jobuler.Application.Exports;
using Jobuler.Application.Notifications;
using Jobuler.Infrastructure.Exports;
using Jobuler.Infrastructure.Notifications;
using Jobuler.Application.AI;
using Jobuler.Application.Auth.Commands;
using Jobuler.Application.Common;
using Jobuler.Application.Scheduling;
using Jobuler.Infrastructure.AI;
using Jobuler.Infrastructure.Auth;
using Jobuler.Infrastructure.Email;
using Jobuler.Application.Auth;
using Jobuler.Infrastructure.Logging;
using Jobuler.Infrastructure.Persistence;
using Jobuler.Infrastructure.Scheduling;
using Jobuler.Infrastructure.Storage;
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

var builder = WebApplication.CreateBuilder(args);

// ─── Serilog ─────────────────────────────────────────────────────────────────
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()   // plain text in dev — easier to read
    .CreateLogger();

builder.Host.UseSerilog();

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

// ─── Application services ────────────────────────────────────────────────────
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(LoginCommand).Assembly);
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
});

builder.Services.AddValidatorsFromAssembly(typeof(LoginCommand).Assembly);

builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddScoped<IPermissionService, PermissionService>();
builder.Services.AddScoped<IAuditLogger, AuditLogger>();
builder.Services.AddScoped<ISystemLogger, SystemLogger>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IPdfRenderer, QuestPdfRenderer>();

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
// ─── File storage ─────────────────────────────────────────────────────────────
// LocalDiskFileStorage for dev — swap for S3FileStorage in prod via config/DI
builder.Services.AddScoped<IFileStorage, LocalDiskFileStorage>();

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

// ─── Scheduling services ─────────────────────────────────────────────────────
builder.Services.AddScoped<ISolverPayloadNormalizer, SolverPayloadNormalizer>();

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
    client.Timeout = TimeSpan.FromSeconds(120);
});

// ─── AI assistant (optional — only registered when API key is configured) ────
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
    // Strict limit on auth endpoints — prevents brute force
    options.AddFixedWindowLimiter("auth", o =>
    {
        o.PermitLimit = builder.Environment.IsDevelopment() ? 100 : 10;
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
builder.Services.AddCors(options =>
    options.AddDefaultPolicy(policy =>
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()));

var app = builder.Build();

// ─── Middleware pipeline ──────────────────────────────────────────────────────
app.UseMiddleware<ExceptionHandlingMiddleware>();
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
