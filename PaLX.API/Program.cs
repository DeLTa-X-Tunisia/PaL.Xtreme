// ============================================================================
// PaL.Xtreme - Modern Instant Messaging Solution
// Copyright © 2026 Azizi Mounir. All Rights Reserved.
// 
// This software is proprietary and confidential.
// Unauthorized copying, distribution, or use is strictly prohibited.
// See LICENSE file for details.
// ============================================================================

using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using PaLX.API.Hubs;
using PaLX.API.Services;
using Serilog;
using Serilog.Events;

// ═══════════════════════════════════════════════════════════════════════════
// CONFIGURATION SERILOG (Logging Structuré)
// ═══════════════════════════════════════════════════════════════════════════
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithProperty("Application", "PaLX.API")
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        path: "logs/palx-api-.log",
        rollingInterval: RollingInterval.Day,
        retainedFileCountLimit: 30,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext}: {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("🚀 Démarrage de PaLX.API...");

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog(); // Utiliser Serilog pour tout le logging ASP.NET Core

// ═══════════════════════════════════════════════════════════════════════════
// CONFIGURATION DES SECRETS (Variables d'environnement OBLIGATOIRES)
// ═══════════════════════════════════════════════════════════════════════════
var dbPassword = Environment.GetEnvironmentVariable("PALX_DB_PASSWORD") 
    ?? throw new InvalidOperationException(
        "❌ PALX_DB_PASSWORD non défini. " +
        "Configurez cette variable d'environnement avant de démarrer l'API. " +
        "Voir .env.example pour plus d'informations.");

var jwtSecretKey = Environment.GetEnvironmentVariable("PALX_JWT_SECRET") 
    ?? throw new InvalidOperationException(
        "❌ PALX_JWT_SECRET non défini. " +
        "Configurez cette variable d'environnement avec une clé de 64+ caractères. " +
        "Voir .env.example pour plus d'informations.");

// Validation de la longueur minimale de la clé JWT (sécurité)
if (jwtSecretKey.Length < 32)
    throw new InvalidOperationException(
        "❌ PALX_JWT_SECRET trop court. Minimum 32 caractères requis pour la sécurité.");

// Override connection string with env variable password
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")?
    .Replace("${DB_PASSWORD}", dbPassword) ?? throw new InvalidOperationException("Connection string missing");
builder.Configuration["ConnectionStrings:DefaultConnection"] = connectionString;

// Override JWT key
var jwtKey = builder.Configuration["Jwt:Key"]?.Replace("${JWT_SECRET_KEY}", jwtSecretKey) ?? jwtSecretKey;
builder.Configuration["Jwt:Key"] = jwtKey;

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddSignalR();
builder.Services.AddOpenApi();
// Note: IUserService est enregistré plus bas avec IAuthService
builder.Services.AddScoped<DatabaseInitializer>();
builder.Services.AddScoped<IRoomService, RoomService>();
builder.Services.AddScoped<IAccessControlService, AccessControlService>();

// ═══════════════════════════════════════════════════════════════════════════
// RATE LIMITING (Protection anti brute-force et DDoS)
// ═══════════════════════════════════════════════════════════════════════════
builder.Services.AddRateLimiter(options =>
{
    // Global rate limit: 100 requests per minute per IP
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 10
            }));

    // Strict rate limit for auth endpoints: 5 attempts per minute
    options.AddFixedWindowLimiter("auth", config =>
    {
        config.PermitLimit = 5;
        config.Window = TimeSpan.FromMinutes(1);
        config.QueueLimit = 0;
    });

    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.ContentType = "application/json";
        await context.HttpContext.Response.WriteAsync(
            "{\"error\": \"Trop de requêtes. Réessayez dans quelques instants.\"}", token);
    };
});

// ═══════════════════════════════════════════════════════════════════════════
// JWT AUTHENTICATION
// ═══════════════════════════════════════════════════════════════════════════
var key = Encoding.UTF8.GetBytes(jwtKey);

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.RequireHttpsMetadata = false;
    options.SaveToken = true;
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(key),
        ValidateIssuer = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidateAudience = true,
        ValidAudience = builder.Configuration["Jwt:Audience"],
        ClockSkew = TimeSpan.Zero
    };

    // Allow SignalR to send the token in the query string
    options.Events = new JwtBearerEvents
    {
        OnMessageReceived = context =>
        {
            var accessToken = context.Request.Query["access_token"];
            var path = context.HttpContext.Request.Path;
            if (!string.IsNullOrEmpty(accessToken) && (path.StartsWithSegments("/chatHub") || path.StartsWithSegments("/roomHub")))
            {
                context.Token = accessToken;
            }
            return Task.CompletedTask;
        }
    };
});

// Register Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddHostedService<StartupService>();

var app = builder.Build();

// Initialize Database
using (var scope = app.Services.CreateScope())
{
    var dbInit = scope.ServiceProvider.GetRequiredService<DatabaseInitializer>();
    await dbInit.InitializeAsync();
}

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// Serilog request logging (requêtes HTTP)
app.UseSerilogRequestLogging(options =>
{
    options.MessageTemplate = "{RequestMethod} {RequestPath} responded {StatusCode} in {Elapsed:0.0000}ms";
    options.GetLevel = (httpContext, elapsed, ex) => 
        ex != null ? LogEventLevel.Error :
        httpContext.Response.StatusCode >= 500 ? LogEventLevel.Error :
        httpContext.Response.StatusCode >= 400 ? LogEventLevel.Warning :
        LogEventLevel.Information;
});

// Rate Limiting Middleware (AVANT auth)
app.UseRateLimiter();

app.UseStaticFiles(); // Enable static files for uploads

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/chatHub");
app.MapHub<RoomHub>("/roomHub");

    Log.Information("✅ PaLX.API démarré avec succès");
    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "❌ Erreur fatale lors du démarrage de PaLX.API");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
