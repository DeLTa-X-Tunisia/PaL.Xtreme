using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using PaLX.API.Hubs;
using PaLX.API.Services;

var builder = WebApplication.CreateBuilder(args);

// ═══════════════════════════════════════════════════════════════════════════
// CONFIGURATION DES SECRETS (Variables d'environnement)
// ═══════════════════════════════════════════════════════════════════════════
var dbPassword = Environment.GetEnvironmentVariable("PALX_DB_PASSWORD") ?? "2012704"; // Fallback pour dev
var jwtSecretKey = Environment.GetEnvironmentVariable("PALX_JWT_SECRET") 
    ?? "k8Xp2sN9vQ4wY7zA1cF6hJ3mR0tU5iO8bL2eD9gK4nW7xZ1qP6"; // 48 chars = 384 bits

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
builder.Services.AddScoped<IUserService, UserService>();
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

// Rate Limiting Middleware (AVANT auth)
app.UseRateLimiter();

app.UseStaticFiles(); // Enable static files for uploads

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<ChatHub>("/chatHub");
app.MapHub<RoomHub>("/roomHub");

app.Run();
