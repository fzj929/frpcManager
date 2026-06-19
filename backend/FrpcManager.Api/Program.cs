using System.Text;
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
        policy.SetIsOriginAllowed(_ => true)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("JWT Key not configured");

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
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

builder.Services.AddHttpClient("FrpcApi", client =>
{
    client.BaseAddress = new Uri(GetFrpcApiBaseUrl(builder.Configuration));
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<ProxyService>();
builder.Services.AddScoped<FrpcApiService>();
builder.Services.AddScoped<WakeOnLanService>();
builder.Services.AddSingleton<TomlService>();
builder.Services.AddHostedService<ChannelExpiryService>();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();


var app = builder.Build();

// Init DB and seed default admin
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.EnsureCreated();

    // Add ExpiresAt column for upgrades from older versions
    try { db.Database.ExecuteSqlRaw("ALTER TABLE Proxies ADD COLUMN ExpiresAt TEXT NULL"); }
    catch { /* Column already exists */ }

    if (!db.Users.Any())
    {
        db.Users.Add(new User
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("admin123"),
            CreatedAt = DateTime.UtcNow
        });
        db.SaveChanges();
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

app.UseCors();

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
