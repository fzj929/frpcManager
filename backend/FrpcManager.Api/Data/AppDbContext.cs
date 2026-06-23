using FrpcManager.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace FrpcManager.Api.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<Proxy> Proxies => Set<Proxy>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<WakeLog> WakeLogs => Set<WakeLog>();
    public DbSet<WakeSchedule> WakeSchedules => Set<WakeSchedule>();
    public DbSet<WakeMacAddress> WakeMacAddresses => Set<WakeMacAddress>();
    public DbSet<HttpsProxyRule> HttpsProxyRules => Set<HttpsProxyRule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(e =>
        {
            e.HasKey(u => u.Id);
            e.HasIndex(u => u.Username).IsUnique();
        });

        modelBuilder.Entity<Proxy>(e =>
        {
            e.HasKey(p => p.Id);
        });

        modelBuilder.Entity<AuditLog>(e =>
        {
            e.HasKey(l => l.Id);
            e.HasIndex(l => l.CreatedAt);
        });

        modelBuilder.Entity<WakeLog>(e =>
        {
            e.HasKey(l => l.Id);
            e.HasIndex(l => l.CreatedAt);
        });

        modelBuilder.Entity<WakeSchedule>(e =>
        {
            e.HasKey(s => s.Id);
            e.HasIndex(s => s.IsEnabled);
        });

        modelBuilder.Entity<WakeMacAddress>(e =>
        {
            e.HasKey(m => m.Id);
            e.Property(m => m.MacAddress).HasMaxLength(17);
            e.Property(m => m.Name).HasMaxLength(100);
            e.HasIndex(m => m.MacAddress).IsUnique();
        });

        modelBuilder.Entity<HttpsProxyRule>(e =>
        {
            e.HasKey(r => r.Id);
            e.HasIndex(r => r.ListenPort).IsUnique();
            e.HasIndex(r => r.IsEnabled);
        });
    }
}
