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

const int DatabaseCompatibilityVersion = 5;

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
builder.Services.AddScoped<UserContextService>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<HttpsProxyRuntimeService>();
builder.Services.AddSingleton<LoginAttemptLimiter>();
builder.Services.AddSingleton<TomlService>();
builder.Services.AddHostedService<ChannelExpiryService>();
builder.Services.AddHostedService<WakeScheduleService>();
builder.Services.AddHostedService<HttpsProxyStartupService>();

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

    if (ShouldInitializeDatabaseCompatibility(db, databaseProvider, DatabaseCompatibilityVersion))
    {
        InitializeDatabaseCompatibility(db, databaseProvider);
        SetDatabaseCompatibilityVersion(db, databaseProvider, DatabaseCompatibilityVersion);
    }

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
                Role = UserRoles.Admin,
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
        webServerAddr = "127.0.0.1";
        //webServerAddr = string.Equals(
        //    Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER"),
        //    "true",
        //    StringComparison.OrdinalIgnoreCase)
        //        ? "host.docker.internal"
        //        : "127.0.0.1";
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
        AddMySqlColumnIfMissing(db, "Proxies", "ExpiresAt", "ALTER TABLE `Proxies` ADD COLUMN `ExpiresAt` datetime(6) NULL");
        AddMySqlColumnIfMissing(db, "Proxies", "CreatedByUserId", "ALTER TABLE `Proxies` ADD COLUMN `CreatedByUserId` int NULL");
        AddMySqlColumnIfMissing(db, "Users", "FailedLoginCount", "ALTER TABLE `Users` ADD COLUMN `FailedLoginCount` int NOT NULL DEFAULT 0");
        AddMySqlColumnIfMissing(db, "Users", "LockedUntil", "ALTER TABLE `Users` ADD COLUMN `LockedUntil` datetime(6) NULL");
        AddMySqlColumnIfMissing(db, "Users", "Role", "ALTER TABLE `Users` ADD COLUMN `Role` varchar(32) NOT NULL DEFAULT 'admin'");
        AddMySqlColumnIfMissing(db, "Users", "IsDisabled", "ALTER TABLE `Users` ADD COLUMN `IsDisabled` tinyint(1) NOT NULL DEFAULT 0");
        AddMySqlColumnIfMissing(db, "Users", "UpdatedAt", "ALTER TABLE `Users` ADD COLUMN `UpdatedAt` datetime(6) NULL");
        AddMySqlColumnIfMissing(db, "WakeSchedules", "ScheduleMode", "ALTER TABLE `WakeSchedules` ADD COLUMN `ScheduleMode` varchar(20) NOT NULL DEFAULT 'daily'");
        AddMySqlColumnIfMissing(db, "WakeSchedules", "DaysOfWeek", "ALTER TABLE `WakeSchedules` ADD COLUMN `DaysOfWeek` varchar(32) NOT NULL DEFAULT ''");
        AddMySqlColumnIfMissing(db, "WakeSchedules", "SpecificDate", "ALTER TABLE `WakeSchedules` ADD COLUMN `SpecificDate` datetime(6) NULL");

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
        if (!MySqlIndexExists(db, "AuditLogs", "IX_AuditLogs_CreatedAt"))
            db.Database.ExecuteSqlRaw("""CREATE INDEX `IX_AuditLogs_CreatedAt` ON `AuditLogs` (`CreatedAt`);""");

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS `WakeLogs` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `MacAddress` longtext NOT NULL,
                `BroadcastAddress` longtext NOT NULL,
                `Port` int NOT NULL,
                `Source` longtext NOT NULL,
                `Username` longtext NOT NULL,
                `IpAddress` longtext NOT NULL,
                `Success` tinyint(1) NOT NULL,
                `Message` longtext NOT NULL,
                `CreatedAt` datetime(6) NOT NULL,
                CONSTRAINT `PK_WakeLogs` PRIMARY KEY (`Id`)
            );
            """);
        if (!MySqlIndexExists(db, "WakeLogs", "IX_WakeLogs_CreatedAt"))
            db.Database.ExecuteSqlRaw("""CREATE INDEX `IX_WakeLogs_CreatedAt` ON `WakeLogs` (`CreatedAt`);""");

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS `WakeSchedules` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `Name` longtext NOT NULL,
                `MacAddress` longtext NOT NULL,
                `BroadcastAddress` longtext NOT NULL,
                `Port` int NOT NULL,
                `TimeOfDay` longtext NOT NULL,
                `ScheduleMode` varchar(20) NOT NULL DEFAULT 'daily',
                `DaysOfWeek` varchar(32) NOT NULL DEFAULT '',
                `SpecificDate` datetime(6) NULL,
                `IsEnabled` tinyint(1) NOT NULL,
                `LastRunAt` datetime(6) NULL,
                `CreatedAt` datetime(6) NOT NULL,
                `UpdatedAt` datetime(6) NULL,
                CONSTRAINT `PK_WakeSchedules` PRIMARY KEY (`Id`)
            );
            """);
        if (!MySqlIndexExists(db, "WakeSchedules", "IX_WakeSchedules_IsEnabled"))
            db.Database.ExecuteSqlRaw("""CREATE INDEX `IX_WakeSchedules_IsEnabled` ON `WakeSchedules` (`IsEnabled`);""");

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS `WakeMacAddresses` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `MacAddress` varchar(17) NOT NULL,
                `Name` varchar(100) NOT NULL,
                `CreatedAt` datetime(6) NOT NULL,
                `UpdatedAt` datetime(6) NULL,
                CONSTRAINT `PK_WakeMacAddresses` PRIMARY KEY (`Id`)
            );
            """);
        if (!MySqlIndexExists(db, "WakeMacAddresses", "IX_WakeMacAddresses_MacAddress"))
            db.Database.ExecuteSqlRaw("""CREATE UNIQUE INDEX `IX_WakeMacAddresses_MacAddress` ON `WakeMacAddresses` (`MacAddress`);""");

        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS `HttpsProxyRules` (
                `Id` int NOT NULL AUTO_INCREMENT,
                `Name` longtext NOT NULL,
                `ListenPort` int NOT NULL,
                `TargetUrl` longtext NOT NULL,
                `CertificateMode` longtext NOT NULL,
                `CertificatePath` longtext NOT NULL,
                `CertificateKeyPath` longtext NOT NULL,
                `CertificatePassword` longtext NOT NULL,
                `Description` longtext NOT NULL,
                `IsEnabled` tinyint(1) NOT NULL,
                `CreatedAt` datetime(6) NOT NULL,
                `UpdatedAt` datetime(6) NULL,
                CONSTRAINT `PK_HttpsProxyRules` PRIMARY KEY (`Id`)
            );
            """);
        AddMySqlColumnIfMissing(db, "HttpsProxyRules", "CertificateKeyPath", "ALTER TABLE `HttpsProxyRules` ADD COLUMN `CertificateKeyPath` varchar(1024) NOT NULL DEFAULT ''");
        AddMySqlColumnIfMissing(db, "HttpsProxyRules", "CreatedByUserId", "ALTER TABLE `HttpsProxyRules` ADD COLUMN `CreatedByUserId` int NULL");
        if (MySqlIndexExists(db, "HttpsProxyRules", "IX_HttpsProxyRules_ListenPort"))
            db.Database.ExecuteSqlRaw("""DROP INDEX `IX_HttpsProxyRules_ListenPort` ON `HttpsProxyRules`;""");
        if (!MySqlIndexExists(db, "HttpsProxyRules", "IX_HttpsProxyRules_ListenPort_NonUnique"))
            db.Database.ExecuteSqlRaw("""CREATE INDEX `IX_HttpsProxyRules_ListenPort_NonUnique` ON `HttpsProxyRules` (`ListenPort`);""");
        if (!MySqlIndexExists(db, "HttpsProxyRules", "IX_HttpsProxyRules_IsEnabled"))
            db.Database.ExecuteSqlRaw("""CREATE INDEX `IX_HttpsProxyRules_IsEnabled` ON `HttpsProxyRules` (`IsEnabled`);""");
        if (!MySqlIndexExists(db, "Proxies", "IX_Proxies_CreatedByUserId"))
            db.Database.ExecuteSqlRaw("""CREATE INDEX `IX_Proxies_CreatedByUserId` ON `Proxies` (`CreatedByUserId`);""");
        if (!MySqlIndexExists(db, "HttpsProxyRules", "IX_HttpsProxyRules_CreatedByUserId"))
            db.Database.ExecuteSqlRaw("""CREATE INDEX `IX_HttpsProxyRules_CreatedByUserId` ON `HttpsProxyRules` (`CreatedByUserId`);""");
        return;
    }

    AddSqliteColumnIfMissing(db, "Proxies", "ExpiresAt", "ALTER TABLE Proxies ADD COLUMN ExpiresAt TEXT NULL");
    AddSqliteColumnIfMissing(db, "Proxies", "CreatedByUserId", "ALTER TABLE Proxies ADD COLUMN CreatedByUserId INTEGER NULL");
    AddSqliteColumnIfMissing(db, "Users", "FailedLoginCount", "ALTER TABLE Users ADD COLUMN FailedLoginCount INTEGER NOT NULL DEFAULT 0");
    AddSqliteColumnIfMissing(db, "Users", "LockedUntil", "ALTER TABLE Users ADD COLUMN LockedUntil TEXT NULL");
    AddSqliteColumnIfMissing(db, "Users", "Role", "ALTER TABLE Users ADD COLUMN Role TEXT NOT NULL DEFAULT 'admin'");
    AddSqliteColumnIfMissing(db, "Users", "IsDisabled", "ALTER TABLE Users ADD COLUMN IsDisabled INTEGER NOT NULL DEFAULT 0");
    AddSqliteColumnIfMissing(db, "Users", "UpdatedAt", "ALTER TABLE Users ADD COLUMN UpdatedAt TEXT NULL");
    AddSqliteColumnIfMissing(db, "WakeSchedules", "ScheduleMode", "ALTER TABLE WakeSchedules ADD COLUMN ScheduleMode TEXT NOT NULL DEFAULT 'daily'");
    AddSqliteColumnIfMissing(db, "WakeSchedules", "DaysOfWeek", "ALTER TABLE WakeSchedules ADD COLUMN DaysOfWeek TEXT NOT NULL DEFAULT ''");
    AddSqliteColumnIfMissing(db, "WakeSchedules", "SpecificDate", "ALTER TABLE WakeSchedules ADD COLUMN SpecificDate TEXT NULL");

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

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "WakeLogs" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_WakeLogs" PRIMARY KEY AUTOINCREMENT,
            "MacAddress" TEXT NOT NULL,
            "BroadcastAddress" TEXT NOT NULL,
            "Port" INTEGER NOT NULL,
            "Source" TEXT NOT NULL,
            "Username" TEXT NOT NULL,
            "IpAddress" TEXT NOT NULL,
            "Success" INTEGER NOT NULL,
            "Message" TEXT NOT NULL,
            "CreatedAt" TEXT NOT NULL
        );
        """);
    db.Database.ExecuteSqlRaw("""CREATE INDEX IF NOT EXISTS "IX_WakeLogs_CreatedAt" ON "WakeLogs" ("CreatedAt");""");

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "WakeSchedules" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_WakeSchedules" PRIMARY KEY AUTOINCREMENT,
            "Name" TEXT NOT NULL,
            "MacAddress" TEXT NOT NULL,
            "BroadcastAddress" TEXT NOT NULL,
            "Port" INTEGER NOT NULL,
            "TimeOfDay" TEXT NOT NULL,
            "ScheduleMode" TEXT NOT NULL DEFAULT 'daily',
            "DaysOfWeek" TEXT NOT NULL DEFAULT '',
            "SpecificDate" TEXT NULL,
            "IsEnabled" INTEGER NOT NULL,
            "LastRunAt" TEXT NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NULL
        );
        """);
    db.Database.ExecuteSqlRaw("""CREATE INDEX IF NOT EXISTS "IX_WakeSchedules_IsEnabled" ON "WakeSchedules" ("IsEnabled");""");

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "WakeMacAddresses" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_WakeMacAddresses" PRIMARY KEY AUTOINCREMENT,
            "MacAddress" TEXT NOT NULL,
            "Name" TEXT NOT NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NULL
        );
        """);
    db.Database.ExecuteSqlRaw("""CREATE UNIQUE INDEX IF NOT EXISTS "IX_WakeMacAddresses_MacAddress" ON "WakeMacAddresses" ("MacAddress");""");

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "HttpsProxyRules" (
            "Id" INTEGER NOT NULL CONSTRAINT "PK_HttpsProxyRules" PRIMARY KEY AUTOINCREMENT,
            "Name" TEXT NOT NULL,
            "ListenPort" INTEGER NOT NULL,
            "TargetUrl" TEXT NOT NULL,
            "CertificateMode" TEXT NOT NULL,
            "CertificatePath" TEXT NOT NULL,
            "CertificateKeyPath" TEXT NOT NULL,
            "CertificatePassword" TEXT NOT NULL,
            "Description" TEXT NOT NULL,
            "IsEnabled" INTEGER NOT NULL,
            "CreatedAt" TEXT NOT NULL,
            "UpdatedAt" TEXT NULL
        );
        """);
    AddSqliteColumnIfMissing(db, "HttpsProxyRules", "CertificateKeyPath", "ALTER TABLE HttpsProxyRules ADD COLUMN CertificateKeyPath TEXT NOT NULL DEFAULT ''");
    AddSqliteColumnIfMissing(db, "HttpsProxyRules", "CreatedByUserId", "ALTER TABLE HttpsProxyRules ADD COLUMN CreatedByUserId INTEGER NULL");
    db.Database.ExecuteSqlRaw("""DROP INDEX IF EXISTS "IX_HttpsProxyRules_ListenPort";""");
    db.Database.ExecuteSqlRaw("""CREATE INDEX IF NOT EXISTS "IX_HttpsProxyRules_ListenPort_NonUnique" ON "HttpsProxyRules" ("ListenPort");""");
    db.Database.ExecuteSqlRaw("""CREATE INDEX IF NOT EXISTS "IX_HttpsProxyRules_IsEnabled" ON "HttpsProxyRules" ("IsEnabled");""");
    db.Database.ExecuteSqlRaw("""CREATE INDEX IF NOT EXISTS "IX_Proxies_CreatedByUserId" ON "Proxies" ("CreatedByUserId");""");
    db.Database.ExecuteSqlRaw("""CREATE INDEX IF NOT EXISTS "IX_HttpsProxyRules_CreatedByUserId" ON "HttpsProxyRules" ("CreatedByUserId");""");
}

static bool ShouldInitializeDatabaseCompatibility(AppDbContext db, string databaseProvider, int targetVersion)
{
    EnsureSchemaStateTable(db, databaseProvider);
    var currentVersionText = GetSchemaStateValue(db, databaseProvider, "DatabaseCompatibilityVersion");
    return !int.TryParse(currentVersionText, out var currentVersion) || currentVersion < targetVersion;
}

static void SetDatabaseCompatibilityVersion(AppDbContext db, string databaseProvider, int version)
{
    SetSchemaStateValue(db, databaseProvider, "DatabaseCompatibilityVersion", version.ToString());
}

static void EnsureSchemaStateTable(AppDbContext db, string databaseProvider)
{
    if (databaseProvider == "mysql")
    {
        db.Database.ExecuteSqlRaw("""
            CREATE TABLE IF NOT EXISTS `__FrpcManagerSchema` (
                `Key` varchar(100) NOT NULL,
                `Value` varchar(100) NOT NULL,
                CONSTRAINT `PK___FrpcManagerSchema` PRIMARY KEY (`Key`)
            );
            """);
        return;
    }

    db.Database.ExecuteSqlRaw("""
        CREATE TABLE IF NOT EXISTS "__FrpcManagerSchema" (
            "Key" TEXT NOT NULL CONSTRAINT "PK___FrpcManagerSchema" PRIMARY KEY,
            "Value" TEXT NOT NULL
        );
        """);
}

static string? GetSchemaStateValue(AppDbContext db, string databaseProvider, string key)
{
    return databaseProvider == "mysql"
        ? db.Database.SqlQuery<string>($"SELECT `Value` AS Value FROM `__FrpcManagerSchema` WHERE `Key` = {key}").FirstOrDefault()
        : db.Database.SqlQuery<string>($"SELECT \"Value\" AS Value FROM \"__FrpcManagerSchema\" WHERE \"Key\" = {key}").FirstOrDefault();
}

static void SetSchemaStateValue(AppDbContext db, string databaseProvider, string key, string value)
{
    if (databaseProvider == "mysql")
    {
        db.Database.ExecuteSql($"""
            INSERT INTO `__FrpcManagerSchema` (`Key`, `Value`)
            VALUES ({key}, {value})
            ON DUPLICATE KEY UPDATE `Value` = VALUES(`Value`);
            """);
        return;
    }

    db.Database.ExecuteSql($"""
        INSERT INTO "__FrpcManagerSchema" ("Key", "Value")
        VALUES ({key}, {value})
        ON CONFLICT("Key") DO UPDATE SET "Value" = excluded."Value";
        """);
}

static void AddSqliteColumnIfMissing(AppDbContext db, string tableName, string columnName, string alterSql)
{
    if (db.Database.SqlQuery<string>(
            $"SELECT name AS Value FROM pragma_table_info({tableName}) WHERE name = {columnName}")
        .Any())
    {
        return;
    }

    db.Database.ExecuteSqlRaw(alterSql);
}

static void AddMySqlColumnIfMissing(AppDbContext db, string tableName, string columnName, string alterSql)
{
    if (db.Database.SqlQuery<string>(
            $"""
            SELECT COLUMN_NAME AS Value
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = {tableName}
              AND COLUMN_NAME = {columnName}
            """)
        .Any())
    {
        return;
    }

    db.Database.ExecuteSqlRaw(alterSql);
}

static bool MySqlIndexExists(AppDbContext db, string tableName, string indexName)
{
    return db.Database.SqlQuery<string>(
            $"""
            SELECT INDEX_NAME AS Value
            FROM INFORMATION_SCHEMA.STATISTICS
            WHERE TABLE_SCHEMA = DATABASE()
              AND TABLE_NAME = {tableName}
              AND INDEX_NAME = {indexName}
            """)
        .Any();
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
