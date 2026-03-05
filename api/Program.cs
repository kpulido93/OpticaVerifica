using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.OpenApi.Models;
using OptimaVerifica.Api.Auth;
using OptimaVerifica.Api.Endpoints;
using OptimaVerifica.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Configuration.AddEnvironmentVariables();
ValidateAdminCredentialsForProduction(builder.Configuration, builder.Environment);

var corsOrigins = ResolveCorsOrigins(builder.Configuration, builder.Environment);
var rateLimitPermitLimit = ResolveIntSetting(builder.Configuration, "RATE_LIMIT_PERMIT_LIMIT", 60, 1);
var rateLimitWindowSeconds = ResolveIntSetting(builder.Configuration, "RATE_LIMIT_WINDOW_SECONDS", 60, 1);
var rateLimitQueueLimit = ResolveIntSetting(builder.Configuration, "RATE_LIMIT_QUEUE_LIMIT", 0, 0);

// Services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo 
    { 
        Title = "Optima Verifica API", 
        Version = "v1",
        Description = "API para consultas de presets basadas en CÉDULA"
    });
    c.AddSecurityDefinition("Basic", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "basic",
        Description = "Basic Authentication"
    });
    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Basic"
                }
            },
            Array.Empty<string>()
        }
    });
});

// Authentication
builder.Services.AddAuthentication("BasicAuthentication")
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("AdminOnly", policy => policy.RequireRole("ADMIN"));
    options.AddPolicy("OperatorOrAbove", policy => policy.RequireRole("ADMIN", "OPERATOR"));
    options.AddPolicy("AnyRole", policy => policy.RequireRole("ADMIN", "OPERATOR", "READER"));
});

// Custom Services
builder.Services.AddSingleton<IDbConnectionFactory, MySqlConnectionFactory>();
builder.Services.AddScoped<IPresetService, PresetService>();
builder.Services.AddScoped<IJobService, JobService>();
builder.Services.AddScoped<ISchemaService, SchemaService>();
builder.Services.AddScoped<IExportService, ExportService>();
builder.Services.AddScoped<IPresetExecutor, PresetExecutor>();
builder.Services.AddScoped<IIdsParserService, IdsParserService>();

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins(corsOrigins)
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Reverse proxy headers
builder.Services.Configure<ForwardedHeadersOptions>(options =>
{
    options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    options.KnownNetworks.Clear();
    options.KnownProxies.Clear();
});

// Rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;

    options.AddPolicy("ApiFixedWindow", context =>
    {
        var partitionKey = GetRateLimitPartitionKey(context);
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => CreateFixedWindowOptions(rateLimitPermitLimit, rateLimitWindowSeconds, rateLimitQueueLimit));
    });

    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        if (!context.Request.Path.StartsWithSegments("/api"))
        {
            return RateLimitPartition.GetNoLimiter("non-api");
        }

        var partitionKey = GetRateLimitPartitionKey(context);
        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey,
            _ => CreateFixedWindowOptions(rateLimitPermitLimit, rateLimitWindowSeconds, rateLimitQueueLimit));
    });
});

var app = builder.Build();

// Middleware
app.UseForwardedHeaders();

if (app.Environment.IsProduction())
{
    app.UseHsts();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.Use(async (context, next) =>
{
    context.Response.OnStarting(() =>
    {
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.Headers["X-Frame-Options"] = "DENY";
        context.Response.Headers["Referrer-Policy"] = "no-referrer";
        return Task.CompletedTask;
    });

    await next();
});

app.UseCors("AllowFrontend");
app.UseRateLimiter();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();

// Map Endpoints
app.MapPresetEndpoints();
app.MapJobEndpoints();
app.MapAdminEndpoints();
app.MapIdsEndpoints();
app.MapHealthEndpoints();

app.Run();

static string[] ResolveCorsOrigins(IConfiguration configuration, IHostEnvironment environment)
{
    var rawOrigins = configuration["CORS_ALLOWED_ORIGINS"];
    if (!string.IsNullOrWhiteSpace(rawOrigins))
    {
        var parsedOrigins = rawOrigins
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(origin => origin.TrimEnd('/'))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (parsedOrigins.Length > 0)
        {
            return parsedOrigins;
        }
    }

    if (environment.IsDevelopment())
    {
        return
        [
            "http://localhost:3000",
            "http://127.0.0.1:3000"
        ];
    }

    throw new InvalidOperationException(
        "CORS_ALLOWED_ORIGINS is required outside Development. Set a CSV list of trusted origins, e.g. https://app.example.com,https://admin.example.com.");
}

static int ResolveIntSetting(IConfiguration configuration, string key, int defaultValue, int minValue)
{
    var rawValue = configuration[key];
    if (string.IsNullOrWhiteSpace(rawValue))
    {
        return defaultValue;
    }

    if (int.TryParse(rawValue, out var parsedValue) && parsedValue >= minValue)
    {
        return parsedValue;
    }

    throw new InvalidOperationException($"{key} must be an integer greater than or equal to {minValue}.");
}

static string GetRateLimitPartitionKey(HttpContext context)
{
    var remoteIp = context.Connection.RemoteIpAddress?.ToString();
    return string.IsNullOrWhiteSpace(remoteIp) ? "unknown" : remoteIp;
}

static FixedWindowRateLimiterOptions CreateFixedWindowOptions(int permitLimit, int windowSeconds, int queueLimit)
{
    return new FixedWindowRateLimiterOptions
    {
        PermitLimit = permitLimit,
        Window = TimeSpan.FromSeconds(windowSeconds),
        QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
        QueueLimit = queueLimit,
        AutoReplenishment = true
    };
}

static void ValidateAdminCredentialsForProduction(IConfiguration configuration, IHostEnvironment environment)
{
    if (!environment.IsProduction())
    {
        return;
    }

    var adminUser = configuration["Auth:AdminUser"];
    var adminPassword = configuration["Auth:AdminPassword"];

    if (string.IsNullOrWhiteSpace(adminUser) || string.IsNullOrWhiteSpace(adminPassword))
    {
        throw new InvalidOperationException(
            "Missing required admin credentials for Production. Set AUTH_ADMIN_USER and AUTH_ADMIN_PASSWORD.");
    }
}
