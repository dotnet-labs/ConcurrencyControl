using System.Reflection;
using ConcurrencyControl.DbContext.Configurations;

namespace ConcurrencyControl.DbContext;

public class MyDbContext : Microsoft.EntityFrameworkCore.DbContext
{
    public const string DbFileName = "ConcurrencyControl.db";
    public DbSet<ConcurrentAccountWithToken> ConcurrentAccountsWithToken { get; protected set; } = null!;
    public DbSet<ConcurrentAccountWithRowVersion> ConcurrentAccountsWithRowVersion { get; protected set; } = null!;
    public DbSet<NonConcurrentAccount> NonConcurrentAccounts { get; protected set; } = null!;

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder = optionsBuilder
            .UseLoggerFactory(Program.MyLoggerFactory)
            .UseSqlite($"Data source={DbFileName}",
                options => { options.MigrationsAssembly(Assembly.GetExecutingAssembly().FullName); });
        base.OnConfiguring(optionsBuilder);
    }


    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        if (Database.IsSqlite())
        {
            modelBuilder.ApplyConfiguration(new ConcurrentAccountWithTokenEntityTypeConfigurationSqlite());
            modelBuilder.ApplyConfiguration(new ConcurrentAccountWithRowVersionEntityTypeConfigurationSqlite());
            modelBuilder.ApplyConfiguration(new NonConcurrentAccountEntityTypeConfigurationSqlite());
        }
        else
        {
            modelBuilder.ApplyConfiguration(new ConcurrentAccountWithTokenEntityTypeConfiguration());
            modelBuilder.ApplyConfiguration(new ConcurrentAccountWithRowVersionEntityTypeConfiguration());
            modelBuilder.ApplyConfiguration(new NonConcurrentAccountEntityTypeConfiguration());
        }
    }

    public static void EnsureDatabaseIsCleaned()
    {
        File.Delete(DbFileName);
    }
}