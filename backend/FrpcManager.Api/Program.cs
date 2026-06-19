using System.Text;
using System.Net;
using System.Threading.RateLimiting;
using FrpcManager.Api.Data;
using FrpcManager.Api.Models;
using FrpcManager.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
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

var databaseProvider = GetDatabaseProvider(builder.Configuration);
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (databaseProvider == "mysql")
    {
        var connectionString = GetMySqlConnectionString(builder.Configuration);
        var versionText = builder.Configuration["Database:MySqlServerVersion"];
        var serverVersion = string.IsNullOrWhiteSpace(versionText)
            ? new MySqlServerVersion(new Version(8, 0, 0))
            : new MySqlServerVersion(Version.Parse(versionText));

        options.UseMySql(connectionString, serverVersion);
    }
    else
    {
        options.UseSqlite(GetRequiredConnectionString(builder.Configuration, "DefaultConnection"));
    }
});

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
builder.Services.AddSingleton<LoginAttemptLimiter>();
builder.Services.AddSingleton<TomlService>();
builder.Services.AddHostedService<ChannelExpiryService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
if (builder.Configuration.GetValue("ForwardedHeaders:Enabled", false))
{
    builder.Services.Configure<ForwardedHeadersOptions>(options =>
    {
        options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        options.KnownNetworks.Clear();
        options.KnownProxies.Clear();

        foreach (var proxy in builder.Configuration.GetSection("ForwardedHeaders:KnownProxies").Get<string[]>() ?? [])
        {
            if (IPAddress.TryParse(proxy, out var ipAddress))
                options.KnownProxies.Add(ipAddress);
        }

        foreach (var network in builder.Configuration.GetSection("ForwardedHeaders:KnownNetworks").Get<string[]>() ?? [])
        {
            if (TryParseCidr(network, out var prefix, out var prefixLength))
                options.KnownNetworks.Add(new Microsoft.AspNetCore.HttpOverrides.IPNetwork(prefix, prefixLength));
        }
    });
}


var app = builder.Build();

// Init DB and first-run storage
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    InitializeDatabaseCompatibility(db, databaseProvider);

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

if (app.Configuration.GetValue("ForwardedHeaders:Enabled", false))
{
    app.UseForwardedHeaders();
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

static string GetDatabaseProvider(IConfiguration configuration)
{
    var provider = configuration["Database:Provider"];
    provider = string.IsNullOrWhiteSpace(provider) ? "sqlite" : provider.Trim().ToLowerInvariant();
    if (provider is not ("sqlite" or "mysql"))
        throw new InvalidOperationException("Database:Provider must be 'sqlite' or 'mysql'.");

    return provider;
}

static string GetRequiredConnectionString(IConfiguration configuration, string name)
{
    var connectionString = configuration.GetConnectionString(name);
    if (string.IsNullOrWhiteSpace(connectionString))
        throw new InvalidOperationException($"Connection string '{name}' is required.");

    return connectionString;
}

static string GetMySqlConnectionString(IConfiguration configuration)
{
    var connectionString = configuration.GetConnectionString("MySql");
    if (!string.IsNullOrWhiteSpace(connectionString))
        return connectionString;

    return GetRequiredConnectionString(configuration, "DefaultConnection");
}

static void InitializeDatabaseCompatibility(AppDbContext db, string databaseProvider)
{
    if (databaseProvider == "mysql")
    {
        // Add ExpiresAt column for upgrades from older versions.
        try { db.Database.ExecuteSqlRaw("ALTER TABLE `Proxies` ADD COLUMN `ExpiresAt` datetime(6) NULL"); }
        catch { /* Column already exists */ }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE `Users` ADD COLUMN `FailedLoginCount` int NOT NULL DEFAULT 0"); }
        catch { /* Column already exists */ }
        try { db.Database.ExecuteSqlRaw("ALTER TABLE `Users` ADD COLUMN `LockedUntil` datetime(6) NULL"); }
        catch { /* Column already exists */ }

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS `AuditLogs` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `Username` longtext NOT NULL,
                `Action` longtext NOT NULL,
                `Target` longtext NOT NULL,
                `Details` longtext NOT NULL,
                `IpAddress` longtext NOT NULL,
                `Success` tinyint(1) NOT NULL,
                `CreatedAt` datetime(6) NOT NULL,
                CONSTRAINT `PK_AuditLogs` PRIMARY KEY (`Id`)
            );
            """);
        try { db.Database.ExecuteSqlRaw("""CREATE INDEX `IX_AuditLogs_CreatedAt` ON `AuditLogs` (`CreatedAt`);"""); }
        catch { /* Index already exists */ }
        return;
    }

    // Add ExpiresAt column for upgrades from older versions.
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Proxies ADD COLUMN ExpiresAt TEXT NULL"); }
    catch { /* Column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN FailedLoginCount INTEGER NOT NULL DEFAULT 0"); }
    catch { /* Column already exists */ }
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Users ADD COLUMN LockedUntil TEXT NULL"); }
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
}

static bool TryParseCidr(string value, out IPAddress prefix, out int prefixLength)
{
    prefix = IPAddress.None;
    prefixLength = 0;

    var parts = value.Split('/', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
    if (parts.Length != 2)
        return false;

    if (!IPAddress.TryParse(parts[0], out var parsedPrefix))
        return false;

    prefix = parsedPrefix;

    if (!int.TryParse(parts[1], out prefixLength))
        return false;

    var maxPrefixLength = prefix.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork ? 32 : 128;
    return prefixLength >= 0 && prefixLength <= maxPrefixLength;
}
