using System.Net.Http;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using UrbanDiagnosticCentre.Data;
using UrbanDiagnosticCentre.Models;
using UrbanDiagnosticCentre.Services;

namespace UrbanDiagnosticCentre.Sync;

/// <summary>
/// Background worker running on the Reception machine.
/// Loops continuously: push outbox → pull log → apply → advance cursor.
/// Exponential backoff on failure. Never blocks the UI thread.
/// </summary>
public sealed class ReceptionSyncWorker
{
    private readonly Func<AppDbContext> _dbFactory;
    private readonly CancellationTokenSource _cts = new();
    private Thread? _thread;
    private HttpClient? _httpClient;
    private string _cachedIdentityKey = string.Empty;

    // Max backoff between failed attempts.
    private static readonly TimeSpan MinDelay    = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan MaxDelay    = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(15);

    private static readonly HashSet<string> _adminOnlyTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        nameof(TestDefinition), nameof(TestPrice),
        nameof(TestPackage), nameof(TestPackageItem),
        nameof(User)
    };

    public ReceptionSyncWorker(Func<AppDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public void Start()
    {
        _thread = new Thread(RunLoop) { IsBackground = true, Name = "SyncWorker" };
        _thread.Start();
    }

    public void Stop()
    {
        _cts.Cancel();
        _httpClient?.Dispose();
        _httpClient = null;
    }

    // ── Main loop ─────────────────────────────────────────────────────────────

    private void RunLoop()
    {
        var delay = MinDelay;

        while (!_cts.IsCancellationRequested)
        {
            try
            {
                using var db = _dbFactory();
                var identity = db.SyncIdentities.FirstOrDefault();
                if (identity is null || string.IsNullOrEmpty(identity.AdminApiBaseUrl))
                {
                    // Not configured yet — wait and retry
                    UpdateStatus(SyncState.Offline, "Admin URL not configured");
                    Sleep(PollInterval);
                    continue;
                }

                var client = GetOrCreateClient(identity);

                UpdateStatus(SyncState.Syncing, "Pushing outbox…");
                PushOutbox(db, client);

                UpdateStatus(SyncState.Syncing, "Pulling changes…");
                PullAndApply(db, client);

                delay = MinDelay;
                var pending = db.SyncOutboxEntries.Count(e => !e.IsSent);
                SyncStatusInfo.Instance.SetOn(() =>
                {
                    SyncStatusInfo.Instance.State              = SyncState.Idle;
                    SyncStatusInfo.Instance.Message            = "Sync complete";
                    SyncStatusInfo.Instance.PendingCount       = pending;
                    SyncStatusInfo.Instance.LastSyncAt         = DateTime.Now;
                    SyncStatusInfo.Instance.ConsecutiveFailures = 0;
                    SyncStatusInfo.Instance.CurrentRetryDelay  = TimeSpan.Zero;
                });
                Sleep(PollInterval);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                var nextDelay = delay < MaxDelay ? TimeSpan.FromSeconds(delay.TotalSeconds * 2) : MaxDelay;
                SyncStatusInfo.Instance.SetOn(() =>
                {
                    SyncStatusInfo.Instance.State              = SyncState.Retrying;
                    SyncStatusInfo.Instance.Message            = $"Sync failed: {ex.Message}";
                    SyncStatusInfo.Instance.ConsecutiveFailures++;
                    SyncStatusInfo.Instance.CurrentRetryDelay  = delay;
                });
                Sleep(delay);
                delay = nextDelay;
            }
        }
    }

    // ── Push ──────────────────────────────────────────────────────────────────

    private static void PushOutbox(AppDbContext db, HttpClient client)
    {
        var pending = db.SyncOutboxEntries
            .Where(e => !e.IsSent)
            .OrderBy(e => e.CreatedAt)
            .Take(100)
            .ToList();

        if (pending.Count == 0) return;

        var sourceMachine = AppDbContext.CurrentMachineCode;
        var payload = pending.Select(e => new
        {
            e.CorrelationId,
            e.EntityType,
            EntitySyncId      = e.EntitySyncId.ToString(),
            e.Operation,
            e.Payload,
            SourceMachineCode = sourceMachine
        }).ToList();

        var json    = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        string responseJson;
        try
        {
            var response = client.PostAsync("/sync/push", content).GetAwaiter().GetResult();
            response.EnsureSuccessStatusCode();
            responseJson = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            // Entire batch failed — record the error per entry and re-throw so RunLoop backs off.
            var errMsg = ex.Message.Length > 400 ? ex.Message[..400] : ex.Message;
            foreach (var e in pending) { e.AttemptCount++; e.LastError = errMsg; }
            db.SaveChanges();
            throw;
        }

        // Parse which correlation IDs the server flagged as retryable (e.g. FK not yet on Admin).
        var retryableIds = new HashSet<Guid>();
        try
        {
            using var doc = JsonDocument.Parse(responseJson);
            if (doc.RootElement.TryGetProperty("RetryableIds", out var arr))
                foreach (var elem in arr.EnumerateArray())
                    if (Guid.TryParse(elem.GetString(), out var g)) retryableIds.Add(g);
        }
        catch { /* ignore malformed response — treat all as accepted */ }

        var now = DateTime.UtcNow;
        foreach (var entry in pending)
        {
            entry.AttemptCount++;
            if (retryableIds.Contains(entry.CorrelationId))
            {
                entry.LastError = "Pending FK resolution on Admin — will retry";
            }
            else
            {
                entry.IsSent    = true;
                entry.SentAt    = now;
                entry.LastError = null;
            }
        }
        db.SaveChanges();
    }

    // ── Pull ──────────────────────────────────────────────────────────────────

    private static void PullAndApply(AppDbContext db, HttpClient client)
    {
        // Get the oldest cursor (or DateTime.MinValue if none)
        var sinceUtc = db.SyncCursors
            .OrderBy(c => c.LastPulledAt)
            .Select(c => (DateTime?)c.LastPulledAt)
            .FirstOrDefault() ?? DateTime.MinValue;

        var url      = $"/sync/pull?since={sinceUtc:o}";
        var response = client.GetAsync(url).GetAwaiter().GetResult();
        response.EnsureSuccessStatusCode();

        var json    = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        var entries = JsonSerializer.Deserialize<List<SyncLogDto>>(json,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (entries is null || entries.Count == 0) return;

        // Apply in one transaction — only advance cursor after successful commit.
        // Suppress outbox capture: applied changes came FROM Admin so must not be re-queued.
        using var tx = db.Database.BeginTransaction();
        AppDbContext.SuppressSyncCapture = true;
        try
        {
            foreach (var entry in entries.OrderBy(e => e.AppliedAt))
                ApplyEntry(db, entry);

            // Advance (or upsert) cursor to the latest AppliedAt we just consumed
            var latest = entries.Max(e => e.AppliedAt);
            var cursor = db.SyncCursors.FirstOrDefault(c => c.EntityType == "__global__");
            if (cursor is null)
            {
                db.SyncCursors.Add(new SyncCursor
                {
                    EntityType   = "__global__",
                    LastPulledAt = latest,
                    UpdatedAt    = DateTime.UtcNow
                });
            }
            else
            {
                cursor.LastPulledAt = latest;
                cursor.UpdatedAt    = DateTime.UtcNow;
            }

            db.SaveChanges();
            tx.Commit();
        }
        finally
        {
            AppDbContext.SuppressSyncCapture = false;
        }
    }

    // ── Apply a single log entry to the local Reception DB ───────────────────

    private static void ApplyEntry(AppDbContext db, SyncLogDto entry)
    {
        if (_adminOnlyTypes.Contains(entry.EntityType))
        {
            // Admin-only: apply pull (read-only from reception perspective = just upsert)
            UpsertAdminOnlyEntity(db, entry);
            return;
        }

        switch (entry.EntityType)
        {
            case nameof(Patient):      ApplyPatient(db, entry);      break;
            case nameof(Report):       ApplyReport(db, entry);       break;
            case nameof(ReportEntry):  ApplyReportEntry(db, entry);  break;
            case nameof(FinancialTransaction): ApplyFinancialTx(db, entry); break;
            default:
                UpsertGenericEntity(db, entry);
                break;
        }
    }

    private static void ApplyPatient(AppDbContext db, SyncLogDto entry)
    {
        var dict   = Deserialize(entry.Payload);
        var syncId = Guid.Parse(dict["SyncId"]!.ToString()!);
        var local  = db.Patients.FirstOrDefault(p => p.SyncId == syncId);

        if (entry.Operation == "Delete")
        {
            if (local is not null) { local.IsArchived = true; local.ArchivedAt = DateTime.UtcNow; }
            return;
        }

        if (local is null)
        {
            local = new Patient { SyncId = syncId };
            db.Patients.Add(local);
        }
        else if (entry.Operation == "Update" && local.SyncId == syncId)
        {
            // Reception cannot overwrite admin's patient demographics with stale local data.
            // But we ARE applying what Admin sent, so it's safe.
        }

        MapPatient(local, dict);
    }

    private static void ApplyReport(AppDbContext db, SyncLogDto entry)
    {
        var dict   = Deserialize(entry.Payload);
        var syncId = Guid.Parse(dict["SyncId"]!.ToString()!);
        var local  = db.Reports.Include(r => r.Entries).FirstOrDefault(r => r.SyncId == syncId);

        if (entry.Operation == "Delete")
        {
            if (local is not null) { local.IsArchived = true; local.ArchivedAt = DateTime.UtcNow; }
            return;
        }

        // Completed/Printed reports are immutable — only accept if local is Draft or null
        if (local is not null && local.Status is ReportStatus.Completed or ReportStatus.Printed)
        {
            if (local.Status == ReportStatus.Printed) return; // fully immutable
            // Completed: Admin may push status changes (e.g. archiving)
        }

        if (local is null)
        {
            local = new Report { SyncId = syncId };
            var patientSyncId = GetGuidNullable(dict, "PatientSyncId");
            if (patientSyncId.HasValue)
                local.PatientId = db.Patients.Where(p => p.SyncId == patientSyncId.Value).Select(p => p.Id).FirstOrDefault();
            db.Reports.Add(local);
        }

        MapReport(local, dict);
    }

    private static void ApplyReportEntry(AppDbContext db, SyncLogDto entry)
    {
        var dict   = Deserialize(entry.Payload);
        var syncId = Guid.Parse(dict["SyncId"]!.ToString()!);
        var local  = db.ReportEntries.FirstOrDefault(e => e.SyncId == syncId);

        if (entry.Operation == "Delete")
        {
            if (local is not null) db.ReportEntries.Remove(local);
            return;
        }

        if (local is null)
        {
            local = new ReportEntry { SyncId = syncId };
            var reportSyncId = GetGuidNullable(dict, "ReportSyncId");
            if (reportSyncId.HasValue)
                local.ReportId = db.Reports.Where(r => r.SyncId == reportSyncId.Value).Select(r => r.Id).FirstOrDefault();
            var testDefSyncId = GetGuidNullable(dict, "TestDefinitionSyncId");
            if (testDefSyncId.HasValue)
                local.TestDefinitionId = db.TestDefinitions.Where(t => t.SyncId == testDefSyncId.Value).Select(t => t.Id).FirstOrDefault();
            db.ReportEntries.Add(local);
        }

        MapReportEntry(local, dict);
    }

    private static void ApplyFinancialTx(AppDbContext db, SyncLogDto entry)
    {
        var dict   = Deserialize(entry.Payload);
        var syncId = Guid.Parse(dict["SyncId"]!.ToString()!);
        var local  = db.FinancialTransactions.FirstOrDefault(ft => ft.SyncId == syncId);

        if (entry.Operation == "Delete")
        {
            // Soft delete only
            if (local is not null) { local.IsDeleted = true; local.DeletedAt = DateTime.UtcNow; }
            return;
        }

        if (local is null)
        {
            local = new FinancialTransaction { SyncId = syncId };
            db.FinancialTransactions.Add(local);
        }
        // Financial transactions are append-only; only apply inserts and soft-deletes
        if (entry.Operation == "Insert") MapFinancialTx(local, dict);
    }

    private static void UpsertAdminOnlyEntity(AppDbContext db, SyncLogDto entry)
    {
        // For admin-only reference data, apply as-is using raw approach per entity type
        var dict   = Deserialize(entry.Payload);
        var syncId = Guid.Parse(dict["SyncId"]!.ToString()!);

        switch (entry.EntityType)
        {
            case nameof(TestDefinition):
                var td = db.TestDefinitions.FirstOrDefault(x => x.SyncId == syncId);
                if (td is null) { td = new TestDefinition { SyncId = syncId }; db.TestDefinitions.Add(td); }
                MapTestDefinition(td, dict);
                break;
            case nameof(TestPackage):
                var tp = db.TestPackages.FirstOrDefault(x => x.SyncId == syncId);
                if (tp is null) { tp = new TestPackage { SyncId = syncId }; db.TestPackages.Add(tp); }
                MapTestPackage(tp, dict);
                break;
            case nameof(TestPackageItem):
                var tpi = db.TestPackageItems.FirstOrDefault(x => x.SyncId == syncId);
                if (tpi is null)
                {
                    tpi = new TestPackageItem { SyncId = syncId };
                    var pkgSyncId = GetGuidNullable(dict, "PackageSyncId");
                    if (pkgSyncId.HasValue)
                        tpi.PackageId = db.TestPackages.Where(x => x.SyncId == pkgSyncId.Value).Select(x => x.Id).FirstOrDefault();
                    var tdSyncId = GetGuidNullable(dict, "TestDefinitionSyncId");
                    if (tdSyncId.HasValue)
                        tpi.TestDefinitionId = db.TestDefinitions.Where(x => x.SyncId == tdSyncId.Value).Select(x => x.Id).FirstOrDefault();
                    db.TestPackageItems.Add(tpi);
                }
                MapTestPackageItem(tpi, dict);
                break;
            case nameof(TestPrice):
                var tpr = db.TestPrices.FirstOrDefault(x => x.SyncId == syncId);
                if (tpr is null)
                {
                    tpr = new TestPrice { SyncId = syncId };
                    var tdSyncId2 = GetGuidNullable(dict, "TestDefinitionSyncId");
                    if (tdSyncId2.HasValue)
                        tpr.TestDefinitionId = db.TestDefinitions.Where(x => x.SyncId == tdSyncId2.Value).Select(x => x.Id).FirstOrDefault();
                    db.TestPrices.Add(tpr);
                }
                MapTestPrice(tpr, dict);
                break;
            case nameof(User):
                var u = db.Users.FirstOrDefault(x => x.SyncId == syncId);
                if (u is null) { u = new User { SyncId = syncId }; db.Users.Add(u); }
                MapUser(u, dict);
                break;
        }
    }

    private static void UpsertGenericEntity(AppDbContext db, SyncLogDto entry)
    {
        // Fallback: log unknown entity types for later investigation
        Services.ErrorLoggingService.LogInfo($"[SyncWorker] Unknown entity type '{entry.EntityType}' — skipped");
    }

    // ── Mapping helpers ───────────────────────────────────────────────────────

    private static void MapPatient(Patient p, Dictionary<string, object?> d)
    {
        p.FullName        = GetStr(d, "FullName");
        p.Age             = GetInt(d, "Age");
        p.Gender          = Enum.Parse<Gender>(GetStr(d, "Gender"));
        p.PhoneNumber     = GetStr(d, "PhoneNumber");
        p.ReferringDoctor = GetStr(d, "ReferringDoctor");
        p.Address         = GetStr(d, "Address");
        p.IsArchived      = GetBool(d, "IsArchived");
        p.UpdatedAt       = GetDateUtc(d, "UpdatedAt") ?? DateTime.UtcNow;
    }

    private static void MapReport(Report r, Dictionary<string, object?> d)
    {
        r.ReportCode     = GetStr(d, "ReportCode");
        r.TestDate       = GetDateLocal(d, "TestDate") ?? DateTime.Now;
        r.ReportDate     = GetDateLocal(d, "ReportDate") ?? DateTime.Now;
        r.Status         = Enum.Parse<ReportStatus>(GetStr(d, "Status"));
        r.BillingMode    = Enum.Parse<BillingMode>(GetStr(d, "BillingMode"));
        r.PackageTotalPrice     = GetDecimal(d, "PackageTotalPrice");
        r.PackageNameSnapshot   = GetStrNullable(d, "PackageNameSnapshot");
        r.PriceTierName         = GetStrNullable(d, "PriceTierName");
        r.IsArchived     = GetBool(d, "IsArchived");
        r.OriginMachineCode = GetStr(d, "OriginMachineCode");
        r.UpdatedAt      = GetDateUtc(d, "UpdatedAt") ?? DateTime.UtcNow;
    }

    private static void MapReportEntry(ReportEntry e, Dictionary<string, object?> d)
    {
        e.ResultValue        = GetStr(d, "ResultValue");
        e.ResultFlag         = Enum.Parse<ResultFlag>(GetStr(d, "ResultFlag"));
        e.ReferenceRangeUsed = GetStr(d, "ReferenceRangeUsed");
        e.PriceTierName      = GetStrNullable(d, "PriceTierName");
        e.IsFromPackage      = GetBool(d, "IsFromPackage");
        // Invariant: package entries must never carry an individual charged price.
        e.ChargedPrice       = e.IsFromPackage ? null : GetDecimal(d, "ChargedPrice");
        e.UpdatedAt          = GetDateUtc(d, "UpdatedAt") ?? DateTime.UtcNow;
    }

    private static void MapFinancialTx(FinancialTransaction ft, Dictionary<string, object?> d)
    {
        ft.TransactionDate = GetDateLocal(d, "TransactionDate") ?? DateTime.Now;
        ft.Type            = Enum.Parse<TransactionType>(GetStr(d, "Type"));
        ft.Category        = GetStr(d, "Category");
        ft.Description     = GetStr(d, "Description");
        ft.Amount          = GetDecimal(d, "Amount") ?? 0m;
        ft.Notes           = GetStrNullable(d, "Notes");
        ft.IsDeleted       = GetBool(d, "IsDeleted");
        ft.DeletedAt       = GetDateUtc(d, "DeletedAt");
        ft.UpdatedAt       = GetDateUtc(d, "UpdatedAt") ?? DateTime.UtcNow;
    }

    private static void MapTestDefinition(TestDefinition td, Dictionary<string, object?> d)
    {
        td.TestName         = GetStr(d, "TestName");
        td.Category         = GetStr(d, "Category");
        td.SampleType       = GetStr(d, "SampleType");
        td.Unit             = GetStr(d, "Unit");
        td.MaleMinValue     = GetDecimal(d, "MaleMinValue") ?? 0m;
        td.MaleMaxValue     = GetDecimal(d, "MaleMaxValue") ?? 0m;
        td.FemaleMinValue   = GetDecimal(d, "FemaleMinValue") ?? 0m;
        td.FemaleMaxValue   = GetDecimal(d, "FemaleMaxValue") ?? 0m;
        td.ChildMinValue    = GetDecimal(d, "ChildMinValue") ?? 0m;
        td.ChildMaxValue    = GetDecimal(d, "ChildMaxValue") ?? 0m;
        td.DecimalPrecision = GetInt(d, "DecimalPrecision");
        td.Notes            = GetStr(d, "Notes");
        td.IsArchived       = GetBool(d, "IsArchived");
        td.UpdatedAt        = GetDateUtc(d, "UpdatedAt") ?? DateTime.UtcNow;
    }

    private static void MapTestPackage(TestPackage tp, Dictionary<string, object?> d)
    {
        tp.Name        = GetStr(d, "Name");
        tp.Description = GetStr(d, "Description");
        tp.Price       = GetDecimal(d, "Price") ?? 0m;
        tp.IsActive    = GetBool(d, "IsActive");
        tp.UpdatedAt   = GetDateUtc(d, "UpdatedAt") ?? DateTime.UtcNow;
    }

    private static void MapTestPackageItem(TestPackageItem tpi, Dictionary<string, object?> d)
    {
        tpi.SortOrder  = GetInt(d, "SortOrder");
        tpi.UpdatedAt  = GetDateUtc(d, "UpdatedAt") ?? DateTime.UtcNow;
    }

    private static void MapTestPrice(TestPrice tp, Dictionary<string, object?> d)
    {
        tp.TierName  = GetStr(d, "TierName");
        tp.Price     = GetDecimal(d, "Price") ?? 0m;
        tp.SortOrder = GetInt(d, "SortOrder");
        tp.IsActive  = GetBool(d, "IsActive");
        tp.UpdatedAt = GetDateUtc(d, "UpdatedAt") ?? DateTime.UtcNow;
    }

    private static void MapUser(User u, Dictionary<string, object?> d)
    {
        u.Username     = GetStr(d, "Username");
        u.PasswordHash = GetStr(d, "PasswordHash");
        u.Role         = GetStr(d, "Role");
        u.FullName     = GetStr(d, "FullName");
        u.IsActive     = GetBool(d, "IsActive");
        u.UpdatedAt    = GetDateUtc(d, "UpdatedAt") ?? DateTime.UtcNow;
    }

    // ── JSON access helpers ───────────────────────────────────────────────────

    private static Dictionary<string, object?> Deserialize(string json)
        => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)!
            .ToDictionary(k => k.Key, v => (object?)v.Value);

    private static string GetStr(Dictionary<string, object?> d, string key)
        => d.TryGetValue(key, out var v) && v is JsonElement e ? e.GetString() ?? string.Empty : string.Empty;

    private static string? GetStrNullable(Dictionary<string, object?> d, string key)
        => d.TryGetValue(key, out var v) && v is JsonElement e ? e.GetString() : null;

    private static int GetInt(Dictionary<string, object?> d, string key)
        => d.TryGetValue(key, out var v) && v is JsonElement e ? e.GetInt32() : 0;

    private static bool GetBool(Dictionary<string, object?> d, string key)
        => d.TryGetValue(key, out var v) && v is JsonElement e && e.GetBoolean();

    private static decimal? GetDecimal(Dictionary<string, object?> d, string key)
    {
        if (!d.TryGetValue(key, out var v) || v is not JsonElement e) return null;
        if (e.ValueKind == JsonValueKind.Null) return null;
        if (e.ValueKind == JsonValueKind.String && decimal.TryParse(e.GetString(), out var sd)) return sd;
        if (e.ValueKind == JsonValueKind.Number && e.TryGetDecimal(out var nd)) return nd;
        return null;
    }

    private static DateTime? GetDateUtc(Dictionary<string, object?> d, string key)
    {
        if (!d.TryGetValue(key, out var v) || v is not JsonElement e) return null;
        if (e.ValueKind == JsonValueKind.Null) return null;
        return DateTime.TryParse(e.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt.ToUniversalTime() : null;
    }

    private static DateTime? GetDateLocal(Dictionary<string, object?> d, string key)
    {
        if (!d.TryGetValue(key, out var v) || v is not JsonElement e) return null;
        if (e.ValueKind == JsonValueKind.Null) return null;
        return DateTime.TryParse(e.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt.ToLocalTime() : null;
    }

    private static Guid? GetGuidNullable(Dictionary<string, object?> d, string key)
    {
        if (!d.TryGetValue(key, out var v) || v is not JsonElement e) return null;
        if (e.ValueKind == JsonValueKind.Null) return null;
        return Guid.TryParse(e.GetString(), out var g) ? g : (Guid?)null;
    }

    // ── Utilities ─────────────────────────────────────────────────────────────

    private HttpClient GetOrCreateClient(SyncIdentity identity)
    {
        var key = $"{identity.AdminApiBaseUrl}|{identity.ApiKey}";
        if (_httpClient is not null && key == _cachedIdentityKey) return _httpClient;

        _httpClient?.Dispose();
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(identity.AdminApiBaseUrl!),
            Timeout     = TimeSpan.FromSeconds(30)
        };
        if (!string.IsNullOrEmpty(identity.ApiKey))
            _httpClient.DefaultRequestHeaders.Add("X-Api-Key", identity.ApiKey);

        _cachedIdentityKey = key;
        return _httpClient;
    }

    private void Sleep(TimeSpan delay)
    {
        try { Task.Delay(delay, _cts.Token).GetAwaiter().GetResult(); }
        catch (OperationCanceledException) { }
    }

    private static void UpdateStatus(SyncState state, string message)
    {
        SyncStatusInfo.Instance.SetOn(() =>
        {
            SyncStatusInfo.Instance.State   = state;
            SyncStatusInfo.Instance.Message = message;
        });
    }
}

// ── DTO for pull response ─────────────────────────────────────────────────────

internal sealed class SyncLogDto
{
    public Guid     CorrelationId     { get; set; }
    public string   EntityType        { get; set; } = string.Empty;
    public Guid     EntitySyncId      { get; set; }
    public string   Operation         { get; set; } = string.Empty;
    public string   Payload           { get; set; } = string.Empty;
    public string   SourceMachineCode { get; set; } = string.Empty;
    public DateTime AppliedAt         { get; set; }
}
