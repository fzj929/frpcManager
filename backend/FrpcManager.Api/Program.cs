using System.Text;
using System.Threading.RateLimiting;
using FrpcManager.Api.Data;
using FrpcManager.Api.Models;
using FrpcManager.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var origins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (origins.Length > 0)
        {
            policy.WithOrigins(origins)
                  .AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials();
        }
        else
        {
            policy.AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

var jwtKeyProvider = new JwtKeyProvider(builder.Configuration);
builder.Services.AddSingleton(jwtKeyProvider);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKeyProvider.Key))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(options =>
{
    options.AddPolicy("login", context =>
        RateLimitPartition.GetFixedWindowLimiter(
            context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            }));
});

builder.Services.AddHttpClient("FrpcApi", client =>
{
    client.BaseAddress = new Uri(GetFrpcApiBaseUrl(builder.Configuration));
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProxyService>();
builder.Services.AddScoped<FrpcApiService>();
builder.Services.AddScoped<WakeOnLanService>();
builder.Services.AddScoped<AuditLogService>();
builder.Services.AddScoped<BackupService>();
builder.Services.AddSingleton<TomlService>();
builder.Services.AddHostedService<ChannelExpiryService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();

// Init DB and first-run storage
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Add ExpiresAt column for upgrades from older versions
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Proxies ADD COLUMN ExpiresAt TEXT NULL"); }
    catch { /* Column already exists */ }
    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "AuditLogs" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_AuditLogs" PRIMARY KEY AUTOINCREMENT,
            "Username" TEXT NOT NULL,
            "Action" TEXT NOT NULL,
            "Target" TEXT NOT NULL,
            "Details" TEXT NOT NULL,
            "IpAddress" TEXT NOT NULL,
            "Success" INTEGER NOT NULL,
            "CreatedAt" TEXT NOT NULL
        );
        """);
    db.Database.ExecuteSqlRaw("""CREATE INDEX IF NOT EXISTS "IX_AuditLogs_CreatedAt" ON "AuditLogs" ("CreatedAt");""");

    if (!db.Users.Any())
    {
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
        var username = app.Configuration["Admin:Username"];
        if (string.IsNullOrWhiteSpace(username))
            username = "admin";

        var password = app.Configuration["Admin:Password"];
        if (!string.IsNullOrWhiteSpace(password))
        {
            db.Users.Add(new User
            {
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                CreatedAt = DateTime.UtcNow
            });
            db.SaveChanges();
            logger.LogInformation("Initial admin user '{Username}' created from environment configuration.", username);
        }
        else
        {
            logger.LogWarning("No admin user exists. Open the web UI to complete first-run setup, or set Admin__Password before startup.");
        }
    }
}

// Background sync from frpc on startup
_ = Task.Run(async () =>
{
    await Task.Delay(2000);
    try
    {
        await using var scope = app.Services.CreateAsyncScope();
        var proxyService = scope.ServiceProvider.GetRequiredService<ProxyService>();
        await proxyService.SyncFromFrpcAsync();
    }
    catch { /* frpc may not be running yet */ }
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseRouting();
app.UseCors();
app.UseRateLimiter();

if (!app.Environment.IsDevelopment())
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

if (!app.Environment.IsDevelopment())
{
    app.MapFallbackToFile("index.html");
}

app.Run();

static string GetFrpcApiBaseUrl(IConfiguration configuration)
{
    var configuredBaseUrl = configuration["Frpc:ApiBaseUrl"];
    if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
        return configuredBaseUrl;

    var webServerAddr = configuration["Frpc:WebServerAddr"];
    if (string.IsNullOrWhiteSpace(webServerAddr))
    {
        webServerAddr = string.Equals(
            Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
            "true",
            StringComparison.OrdinalIgnoreCase)
                ? "host.docker.internal"
                : "127.0.0.1";
    }

    var webServerPort = configuration.GetValue("Frpc:WebServerPort", 7400);
    return $"http://{webServerAddr}:{webServerPort}";
}
