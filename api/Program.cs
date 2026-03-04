using Microsoft.AspNetCore.Authentication;
using Microsoft.OpenApi.Models;
using OptimaVerifica.Api.Auth;
using OptimaVerifica.Api.Services;
using OptimaVerifica.Api.Endpoints;

var builder = WebApplication.CreateBuilder(args);

// Configuration
builder.Configuration.AddEnvironmentVariables();

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

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Middleware
app.UseCors("AllowFrontend");

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
app.MapHealthEndpoints();

app.Run();
