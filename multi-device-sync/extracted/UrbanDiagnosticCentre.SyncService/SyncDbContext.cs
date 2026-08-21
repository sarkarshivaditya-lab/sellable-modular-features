using Microsoft.EntityFrameworkCore;
using UrbanDiagnosticCentre.SyncService.Models;

namespace UrbanDiagnosticCentre.SyncService;

/// <summary>
/// Lightweight DbContext for the Sync Service. Accesses the same SQLite file
/// as the WPF application (WAL mode). Contains only the tables needed for sync.
/// Does NOT run migrations — migrations are owned by the WPF app.
/// </summary>
public class SyncDbContext : DbContext
{
    public SyncDbContext(DbContextOptions<SyncDbContext> options) : base(options) { }

    public DbSet<SyncPatient>             Patients             { get; set; }
    public DbSet<SyncReport>              Reports              { get; set; }
    public DbSet<SyncReportEntry>         ReportEntries        { get; set; }
    public DbSet<SyncTestDefinition>      TestDefinitions      { get; set; }
    public DbSet<SyncTestPrice>           TestPrices           { get; set; }
    public DbSet<SyncTestPackage>         TestPackages         { get; set; }
    public DbSet<SyncTestPackageItem>     TestPackageItems     { get; set; }
    public DbSet<SyncFinancialTransaction> FinancialTransactions { get; set; }
    public DbSet<SyncUser>                Users                { get; set; }
    public DbSet<SyncIdentityRow>         SyncIdentities       { get; set; }
    public DbSet<AppSyncLogRow>           AppSyncLogEntries    { get; set; }
    public DbSet<SyncOutboxRow>           SyncOutboxEntries    { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SyncPatient>().ToTable("Patients").HasKey(x => x.Id);
        modelBuilder.Entity<SyncPatient>().HasIndex(x => x.SyncId).IsUnique();

        modelBuilder.Entity<SyncReport>().ToTable("Reports").HasKey(x => x.Id);
        modelBuilder.Entity<SyncReport>().HasIndex(x => x.SyncId).IsUnique();
        modelBuilder.Entity<SyncReport>().Property(x => x.BillingMode).HasDefaultValue("Normal");
        modelBuilder.Entity<SyncReport>().Property(x => x.Status).HasDefaultValue("Draft");

        modelBuilder.Entity<SyncReportEntry>().ToTable("ReportEntries").HasKey(x => x.Id);
        modelBuilder.Entity<SyncReportEntry>().HasIndex(x => x.SyncId).IsUnique();

        modelBuilder.Entity<SyncTestDefinition>().ToTable("TestDefinitions").HasKey(x => x.Id);
        modelBuilder.Entity<SyncTestDefinition>().HasIndex(x => x.SyncId).IsUnique();

        modelBuilder.Entity<SyncTestPrice>().ToTable("TestPrices").HasKey(x => x.Id);
        modelBuilder.Entity<SyncTestPrice>().HasIndex(x => x.SyncId).IsUnique();

        modelBuilder.Entity<SyncTestPackage>().ToTable("TestPackages").HasKey(x => x.Id);
        modelBuilder.Entity<SyncTestPackage>().HasIndex(x => x.SyncId).IsUnique();

        modelBuilder.Entity<SyncTestPackageItem>().ToTable("TestPackageItems").HasKey(x => x.Id);
        modelBuilder.Entity<SyncTestPackageItem>().HasIndex(x => x.SyncId).IsUnique();

        modelBuilder.Entity<SyncFinancialTransaction>().ToTable("FinancialTransactions").HasKey(x => x.Id);
        modelBuilder.Entity<SyncFinancialTransaction>().HasIndex(x => x.SyncId).IsUnique();
        modelBuilder.Entity<SyncFinancialTransaction>().Property(x => x.IsDeleted).HasDefaultValue(false);

        modelBuilder.Entity<SyncUser>().ToTable("Users").HasKey(x => x.Id);
        modelBuilder.Entity<SyncUser>().HasIndex(x => x.SyncId).IsUnique();

        modelBuilder.Entity<SyncIdentityRow>().ToTable("SyncIdentities").HasKey(x => x.Id);
        modelBuilder.Entity<SyncIdentityRow>().Property(x => x.Id).ValueGeneratedNever();

        modelBuilder.Entity<AppSyncLogRow>().ToTable("AppSyncLogEntries").HasKey(x => x.Id);
        modelBuilder.Entity<AppSyncLogRow>().HasIndex(x => x.AppliedAt);

        modelBuilder.Entity<SyncOutboxRow>().ToTable("SyncOutboxEntries").HasKey(x => x.Id);
        modelBuilder.Entity<SyncOutboxRow>().HasIndex(x => x.CorrelationId).IsUnique();
    }
}
