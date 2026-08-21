using System.IO;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UrbanDiagnosticCentre.Models;

namespace UrbanDiagnosticCentre.Data;

public class AppDbContext : DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Patient> Patients { get; set; }
    public DbSet<TestDefinition> TestDefinitions { get; set; }
    public DbSet<TestPrice> TestPrices { get; set; }
    public DbSet<Report> Reports { get; set; }
    public DbSet<ReportEntry> ReportEntries { get; set; }
    public DbSet<BackupRecord> BackupRecords { get; set; }
    public DbSet<AppSettings>          AppSettings           { get; set; }
    public DbSet<FinancialTransaction> FinancialTransactions { get; set; }
    public DbSet<TestPackage>     TestPackages     { get; set; }
    public DbSet<TestPackageItem> TestPackageItems { get; set; }

    // Sync infrastructure
    public DbSet<SyncIdentity>    SyncIdentities    { get; set; }
    public DbSet<SyncCursor>      SyncCursors       { get; set; }
    public DbSet<SyncOutboxEntry> SyncOutboxEntries { get; set; }
    public DbSet<AppSyncLogEntry> AppSyncLogEntries { get; set; }

    // ── Machine identity (cached at startup) ──────────────────────────────────
    public static string CurrentMachineCode { get; private set; } = "ADM";
    public static void SetMachineCode(string code) => CurrentMachineCode = code;

    // Thread-local suppression flag — prevents sync entry writes from recursing
    // and allows the ReceptionSyncWorker to apply pull results without re-queuing.
    [ThreadStatic]
    private static bool _suppressSyncCapture;

    public static bool SuppressSyncCapture
    {
        get => _suppressSyncCapture;
        set => _suppressSyncCapture = value;
    }

    private static readonly HashSet<string> _excludedFromSync = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(SyncIdentity), nameof(SyncCursor),
        nameof(SyncOutboxEntry), nameof(AppSyncLogEntry),
        nameof(BackupRecord), nameof(AppSettings)
    };

    private static readonly JsonSerializerOptions _jsonOpts = new() { WriteIndented = false };

    public AppDbContext() { }
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (options.IsConfigured) return;

        var folder   = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        var dbFolder = Path.Combine(folder, "UrbanDiagnosticCentre");
        Directory.CreateDirectory(dbFolder);
        var dbPath = Path.Combine(dbFolder, "udc.db");
        // SQLite journal mode is configured at startup using PRAGMA so the connection string remains provider-compatible.
        options.UseSqlite($"Data Source={dbPath}");
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>().HasIndex(u => u.Username).IsUnique();
        modelBuilder.Entity<User>().HasIndex(u => u.SyncId).IsUnique();

        modelBuilder.Entity<Patient>().Property(p => p.Gender).HasConversion<string>();
        modelBuilder.Entity<Patient>().HasIndex(p => p.FullName);
        modelBuilder.Entity<Patient>().HasIndex(p => p.SyncId).IsUnique();
        modelBuilder.Entity<Patient>()
            .HasOne(p => p.CreatedByUser).WithMany()
            .HasForeignKey(p => p.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Report>().Property(r => r.Status).HasConversion<string>().HasDefaultValue(ReportStatus.Draft);
        modelBuilder.Entity<Report>().Property(r => r.PdfVersion).HasDefaultValue(0);
        modelBuilder.Entity<Report>().Property(r => r.BillingMode).HasConversion<string>().HasDefaultValue(BillingMode.Normal);
        modelBuilder.Entity<Report>().Property(r => r.PackageTotalPrice).HasColumnType("TEXT");
        modelBuilder.Entity<Report>().Property(r => r.OriginMachineCode).HasDefaultValue("ADM");
        modelBuilder.Entity<Report>().HasIndex(r => r.ReportCode).IsUnique();
        modelBuilder.Entity<Report>().HasIndex(r => r.TestDate);
        modelBuilder.Entity<Report>().HasIndex(r => r.SyncId).IsUnique();
        modelBuilder.Entity<Report>()
            .HasOne(r => r.Patient).WithMany(p => p.Reports)
            .HasForeignKey(r => r.PatientId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Report>()
            .HasOne(r => r.CreatedByUser).WithMany()
            .HasForeignKey(r => r.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<Report>()
            .HasOne(r => r.ModifiedByUser).WithMany()
            .HasForeignKey(r => r.ModifiedByUserId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<ReportEntry>().Property(re => re.IsFromPackage).HasDefaultValue(false);
        modelBuilder.Entity<ReportEntry>().Property(re => re.ResultFlag).HasConversion<string>();
        modelBuilder.Entity<ReportEntry>().HasIndex(re => re.SyncId).IsUnique();
        modelBuilder.Entity<ReportEntry>()
            .HasOne(re => re.Report).WithMany(r => r.Entries)
            .HasForeignKey(re => re.ReportId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<ReportEntry>()
            .HasOne(re => re.TestDefinition).WithMany()
            .HasForeignKey(re => re.TestDefinitionId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<BackupRecord>().Property(b => b.BackupSizeBytes).HasDefaultValue(0L);
        modelBuilder.Entity<BackupRecord>().Property(b => b.IsAutoBackup).HasDefaultValue(false);
        modelBuilder.Entity<BackupRecord>().Property(b => b.Note).HasDefaultValue("");

        modelBuilder.Entity<TestPrice>()
            .HasOne(tp => tp.TestDefinition).WithMany()
            .HasForeignKey(tp => tp.TestDefinitionId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TestPrice>().HasIndex(tp => new { tp.TestDefinitionId, tp.TierName }).IsUnique();
        modelBuilder.Entity<TestPrice>().HasIndex(tp => tp.SyncId).IsUnique();
        modelBuilder.Entity<TestPrice>().Property(tp => tp.IsActive).HasDefaultValue(true);
        modelBuilder.Entity<TestPrice>().Property(tp => tp.SortOrder).HasDefaultValue(0);

        modelBuilder.Entity<FinancialTransaction>().Property(ft => ft.Type).HasConversion<string>().HasDefaultValue(TransactionType.Expense);
        modelBuilder.Entity<FinancialTransaction>().Property(ft => ft.Amount).HasColumnType("TEXT");
        modelBuilder.Entity<FinancialTransaction>().Property(ft => ft.TaxAmount).HasColumnType("TEXT");
        modelBuilder.Entity<FinancialTransaction>().Property(ft => ft.IsDeleted).HasDefaultValue(false);
        modelBuilder.Entity<FinancialTransaction>().HasIndex(ft => ft.SyncId).IsUnique();
        modelBuilder.Entity<FinancialTransaction>()
            .HasOne(ft => ft.CreatedByUser).WithMany()
            .HasForeignKey(ft => ft.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        modelBuilder.Entity<FinancialTransaction>().HasIndex(ft => ft.TransactionDate);
        modelBuilder.Entity<FinancialTransaction>().HasIndex(ft => ft.Type);

        modelBuilder.Entity<TestPackage>().Property(p => p.Description).HasDefaultValue("");
        modelBuilder.Entity<TestPackage>().Property(p => p.IsActive).HasDefaultValue(true);
        modelBuilder.Entity<TestPackage>().Property(p => p.Price).HasColumnType("TEXT");
        modelBuilder.Entity<TestPackage>().HasIndex(p => p.Name);
        modelBuilder.Entity<TestPackage>().HasIndex(p => p.SyncId).IsUnique();

        modelBuilder.Entity<TestPackageItem>().Property(i => i.SortOrder).HasDefaultValue(0);
        modelBuilder.Entity<TestPackageItem>().HasIndex(i => i.SyncId).IsUnique();
        modelBuilder.Entity<TestPackageItem>()
            .HasOne(i => i.Package).WithMany(p => p.Items)
            .HasForeignKey(i => i.PackageId).OnDelete(DeleteBehavior.Cascade);
        modelBuilder.Entity<TestPackageItem>()
            .HasOne(i => i.TestDefinition).WithMany()
            .HasForeignKey(i => i.TestDefinitionId).OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<TestDefinition>().HasIndex(td => td.SyncId).IsUnique();

        modelBuilder.Entity<AppSettings>().Property(s => s.Id).ValueGeneratedNever();
        modelBuilder.Entity<AppSettings>().Property(s => s.CentreName).HasDefaultValue("Urban Diagnostic Centre");
        modelBuilder.Entity<AppSettings>().Property(s => s.CentreAddress).HasDefaultValue("");
        modelBuilder.Entity<AppSettings>().Property(s => s.CentrePhone).HasDefaultValue("");
        modelBuilder.Entity<AppSettings>().Property(s => s.CentreEmail).HasDefaultValue("");
        modelBuilder.Entity<AppSettings>().Property(s => s.GstTaxId).HasDefaultValue("");
        modelBuilder.Entity<AppSettings>().Property(s => s.SignatureFooterText).HasDefaultValue("");
        modelBuilder.Entity<AppSettings>().Property(s => s.WatermarkText).HasDefaultValue("");
        modelBuilder.Entity<AppSettings>().Property(s => s.DefaultPriceTier).HasDefaultValue("");
        modelBuilder.Entity<AppSettings>().Property(s => s.LabInchargeName).HasDefaultValue("");
        modelBuilder.Entity<AppSettings>().Property(s => s.ConsultantPathologistName).HasDefaultValue("");

        modelBuilder.Entity<SyncIdentity>().Property(s => s.Id).ValueGeneratedNever();
        modelBuilder.Entity<SyncIdentity>().Property(s => s.MachineCode).HasDefaultValue("ADM");
        modelBuilder.Entity<SyncCursor>().HasIndex(c => c.EntityType).IsUnique();
        modelBuilder.Entity<SyncOutboxEntry>().Property(o => o.AttemptCount).HasDefaultValue(0);
        modelBuilder.Entity<SyncOutboxEntry>().Property(o => o.IsSent).HasDefaultValue(false);
        modelBuilder.Entity<SyncOutboxEntry>().HasIndex(o => o.CorrelationId).IsUnique();
        modelBuilder.Entity<SyncOutboxEntry>().HasIndex(o => o.IsSent);
        modelBuilder.Entity<AppSyncLogEntry>().HasIndex(l => l.AppliedAt);
        modelBuilder.Entity<AppSyncLogEntry>().HasIndex(l => l.EntityType);
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess)
    {
        if (!_suppressSyncCapture) RunSyncCapture();
        return base.SaveChanges(acceptAllChangesOnSuccess);
    }

    public override async Task<int> SaveChangesAsync(
        bool acceptAllChangesOnSuccess, CancellationToken ct = default)
    {
        if (!_suppressSyncCapture) RunSyncCapture();
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, ct);
    }

    private void RunSyncCapture()
    {
        _suppressSyncCapture = true;
        try
        {
            StampSyncFields();
            foreach (var e in BuildSyncEntries()) Add(e);
        }
        finally
        {
            _suppressSyncCapture = false;
        }
    }

    private void StampSyncFields()
    {
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified)) continue;
            var typeName = entry.Entity.GetType().Name;
            if (_excludedFromSync.Contains(typeName)) continue;
            if (entry.Entity.GetType().GetProperty("UpdatedAt") is { } up)
                up.SetValue(entry.Entity, now);
            if (entry.Entity is Report report && entry.State == EntityState.Added
                && string.IsNullOrEmpty(report.OriginMachineCode))
                report.OriginMachineCode = CurrentMachineCode;
        }
    }

    private List<object> BuildSyncEntries()
    {
        var results = new List<object>();
        var code = CurrentMachineCode;
        if (code != "ADM" && code != "RCP") return results;
        var now = DateTime.UtcNow;
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted)) continue;
            var typeName = entry.Entity.GetType().Name;
            if (_excludedFromSync.Contains(typeName)) continue;
            if (entry.Entity.GetType().GetProperty("SyncId") is not { } syncProp) continue;
            var syncId = (Guid)syncProp.GetValue(entry.Entity)!;
            var operation = entry.State switch
            {
                EntityState.Added => "Insert",
                EntityState.Modified => "Update",
                EntityState.Deleted => "Delete",
                _ => "Update"
            };
            string payload;
            try { payload = BuildPayload(entry.Entity); }
            catch { continue; }
            if (code == "ADM")
            {
                results.Add(new AppSyncLogEntry
                {
                    CorrelationId = Guid.NewGuid(),
                    EntityType = typeName,
                    EntitySyncId = syncId,
                    Operation = operation,
                    Payload = payload,
                    SourceMachineCode = code,
                    AppliedAt = now
                });
            }
            else
            {
                results.Add(new SyncOutboxEntry
                {
                    CorrelationId = Guid.NewGuid(),
                    EntityType = typeName,
                    EntitySyncId = syncId,
                    Operation = operation,
                    Payload = payload,
                    CreatedAt = now
                });
            }
        }
        return results;
    }

    private string BuildPayload(object entity)
    {
        var dict = ScalarProps(entity);
        switch (entity)
        {
            case Report r:
                dict["PatientSyncId"] = NavOrFindSyncId(r.Patient, r.PatientId, Patients);
                dict["CreatedByUserSyncId"] = r.CreatedByUserId.HasValue ? NavOrFindSyncId(null, r.CreatedByUserId.Value, Users) : null;
                dict["ModifiedByUserSyncId"] = r.ModifiedByUserId.HasValue ? NavOrFindSyncId(null, r.ModifiedByUserId.Value, Users) : null;
                break;
            case ReportEntry re:
                dict["ReportSyncId"] = NavOrFindSyncId(re.Report, re.ReportId, Reports);
                dict["TestDefinitionSyncId"] = NavOrFindSyncId(re.TestDefinition, re.TestDefinitionId, TestDefinitions);
                break;
            case TestPrice tp:
                dict["TestDefinitionSyncId"] = NavOrFindSyncId(tp.TestDefinition, tp.TestDefinitionId, TestDefinitions);
                break;
            case TestPackageItem tpi:
                dict["PackageSyncId"] = NavOrFindSyncId(tpi.Package, tpi.PackageId, TestPackages);
                dict["TestDefinitionSyncId"] = NavOrFindSyncId(tpi.TestDefinition, tpi.TestDefinitionId, TestDefinitions);
                break;
            case FinancialTransaction ft:
                dict["CreatedByUserSyncId"] = ft.CreatedByUserId.HasValue ? NavOrFindSyncId(null, ft.CreatedByUserId.Value, Users) : null;
                break;
            case Patient p:
                dict["CreatedByUserSyncId"] = p.CreatedByUserId.HasValue ? NavOrFindSyncId(null, p.CreatedByUserId.Value, Users) : null;
                break;
        }
        return JsonSerializer.Serialize(dict, _jsonOpts);
    }

    private static Guid? NavOrFindSyncId<T>(T? navigationEntity, int fkId, DbSet<T> set)
        where T : class
    {
        if (navigationEntity is not null)
            return (Guid?)navigationEntity.GetType().GetProperty("SyncId")?.GetValue(navigationEntity);
        if (fkId <= 0) return null;
        var local = set.Local.FirstOrDefault(e =>
            (int?)e.GetType().GetProperty("Id")?.GetValue(e) == fkId);
        if (local is not null)
            return (Guid?)local.GetType().GetProperty("SyncId")?.GetValue(local);
        var found = set.Find(fkId);
        return (Guid?)found?.GetType().GetProperty("SyncId")?.GetValue(found);
    }

    private static Dictionary<string, object?> ScalarProps(object entity)
    {
        var dict = new Dictionary<string, object?>();
        foreach (var prop in entity.GetType().GetProperties())
        {
            if (!prop.CanRead) continue;
            var type = prop.PropertyType;
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>)) continue;
            if (type.Namespace?.StartsWith("UrbanDiagnosticCentre.Models") == true && !type.IsEnum) continue;
            dict[prop.Name] = prop.GetValue(entity);
        }
        return dict;
    }
}
