using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using UrbanDiagnosticCentre.SyncService.Models;

namespace UrbanDiagnosticCentre.SyncService;

[ApiController]
[Route("sync")]
public class SyncController : ControllerBase
{
    private readonly IDbContextFactory<SyncDbContext> _factory;
    private readonly ILogger<SyncController> _logger;

    // Entity types that only Admin may push — Reception pushes for these are rejected.
    private static readonly HashSet<string> AdminOnlyTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "TestDefinition", "TestPrice", "TestPackage", "TestPackageItem", "User"
    };

    public SyncController(IDbContextFactory<SyncDbContext> factory, ILogger<SyncController> logger)
    {
        _factory = factory;
        _logger  = logger;
    }

    // ── Health ────────────────────────────────────────────────────────────────

    [HttpGet("health")]
    public IActionResult Health()
    {
        using var db       = _factory.CreateDbContext();
        var identity       = db.SyncIdentities.FirstOrDefault();
        var pendingInbound = db.SyncOutboxEntries.Count(e => !e.IsSent);
        return Ok(new
        {
            Status          = "ok",
            MachineCode     = identity?.MachineCode ?? "unknown",
            Timestamp       = DateTime.UtcNow,
            PendingInbound  = pendingInbound
        });
    }

    // ── Push (Reception → Admin) ──────────────────────────────────────────────

    [HttpPost("push")]
    public IActionResult Push([FromBody] List<PushEntryDto> entries)
    {
        if (entries is null || entries.Count == 0) return Ok(new { Accepted = 0 });

        using var db  = _factory.CreateDbContext();
        using var tx  = db.Database.BeginTransaction();

        int accepted = 0;
        int rejected = 0;
        var now          = DateTime.UtcNow;
        var retryableIds = new List<string>();

        foreach (var entry in entries)
        {
            try
            {
                var result = ApplyPushEntry(db, entry, now);
                if (result.Accepted)  accepted++;
                else
                {
                    rejected++;
                    if (result.Retryable) retryableIds.Add(entry.CorrelationId.ToString());
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to apply push entry {CorrelationId} ({EntityType})",
                    entry.CorrelationId, entry.EntityType);
                tx.Rollback();
                return StatusCode(500, new { Error = ex.Message, entry.CorrelationId });
            }
        }

        db.SaveChanges();
        tx.Commit();

        return Ok(new { Accepted = accepted, Rejected = rejected, RetryableIds = retryableIds });
    }

    // ── Pull (Admin → Reception) ──────────────────────────────────────────────

    [HttpGet("pull")]
    public IActionResult Pull([FromQuery] DateTime since)
    {
        using var db = _factory.CreateDbContext();

        var entries = db.AppSyncLogEntries
            .Where(e => e.AppliedAt > since)
            .OrderBy(e => e.AppliedAt)
            .Take(500)
            .Select(e => new
            {
                e.CorrelationId,
                e.EntityType,
                e.EntitySyncId,
                e.Operation,
                e.Payload,
                e.SourceMachineCode,
                e.AppliedAt
            })
            .ToList();

        return Ok(entries);
    }

    // ── Apply logic ───────────────────────────────────────────────────────────

    private (bool Accepted, string Reason, bool Retryable) ApplyPushEntry(SyncDbContext db, PushEntryDto entry, DateTime now)
    {
        // Idempotency: if this CorrelationId was already processed, skip silently.
        if (db.AppSyncLogEntries.Any(l => l.CorrelationId == entry.CorrelationId))
            return (true, "duplicate — already applied", false);

        // Reject admin-only entity types from Reception pushes.
        if (AdminOnlyTypes.Contains(entry.EntityType))
        {
            _logger.LogWarning("Rejected Reception push for admin-only entity {EntityType}", entry.EntityType);
            return (false, $"{entry.EntityType} is admin-only", false);
        }

        var dict = Deserialize(entry.Payload);

        var conflictResult = entry.EntityType switch
        {
            "Patient"              => ApplyPatient(db, entry, dict, now),
            "Report"               => ApplyReport(db, entry, dict, now),
            "ReportEntry"          => ApplyReportEntry(db, entry, dict, now),
            "FinancialTransaction" => ApplyFinancialTransaction(db, entry, dict, now),
            _                     => (Accepted: false, Reason: $"Unknown entity type: {entry.EntityType}", Retryable: false)
        };

        if (!conflictResult.Accepted) return conflictResult;

        // Log the accepted change so Reception can pull it back (and other Reception machines learn of it).
        db.AppSyncLogEntries.Add(new AppSyncLogRow
        {
            CorrelationId     = entry.CorrelationId,
            EntityType        = entry.EntityType,
            EntitySyncId      = entry.EntitySyncId,
            Operation         = entry.Operation,
            Payload           = entry.Payload,
            SourceMachineCode = entry.SourceMachineCode,
            AppliedAt         = now
        });

        return conflictResult;
    }

    private (bool Accepted, string Reason, bool Retryable) ApplyPatient(
        SyncDbContext db, PushEntryDto entry, Dictionary<string, JsonElement> dict, DateTime now)
    {
        var syncId = entry.EntitySyncId;
        var local  = db.Patients.FirstOrDefault(p => p.SyncId == syncId);

        if (entry.Operation == "Delete")
        {
            if (local is not null) { local.IsArchived = true; local.ArchivedAt = now; local.UpdatedAt = now; }
            return (true, "ok", false);
        }

        // Reception can create patients (Insert) but cannot edit demographics (Update).
        if (entry.Operation == "Update" && local is not null)
        {
            _logger.LogWarning("Rejected Reception patient demographic update for SyncId {SyncId}", syncId);
            return (false, "Patient demographic edits are admin-only", false);
        }

        if (local is null)
        {
            local = new SyncPatient { SyncId = syncId };
            db.Patients.Add(local);
        }

        local.FullName        = GetStr(dict, "FullName");
        local.Age             = GetInt(dict, "Age");
        local.Gender          = GetStr(dict, "Gender");
        local.PhoneNumber     = GetStr(dict, "PhoneNumber");
        local.ReferringDoctor = GetStr(dict, "ReferringDoctor");
        local.Address         = GetStr(dict, "Address");
        local.IsArchived      = GetBool(dict, "IsArchived");
        local.UpdatedAt       = now;
        return (true, "ok", false);
    }

    private (bool Accepted, string Reason, bool Retryable) ApplyReport(
        SyncDbContext db, PushEntryDto entry, Dictionary<string, JsonElement> dict, DateTime now)
    {
        var syncId = entry.EntitySyncId;
        var local  = db.Reports.FirstOrDefault(r => r.SyncId == syncId);

        if (entry.Operation == "Delete")
        {
            if (local is not null) { local.IsArchived = true; local.ArchivedAt = now; local.UpdatedAt = now; }
            return (true, "ok", false);
        }

        // Completed and Printed reports are immutable.
        if (local is not null && (local.Status == "Completed" || local.Status == "Printed"))
            return (false, $"Report {syncId} is {local.Status} — immutable", false);

        // Draft reports can only be edited by the origin machine.
        var originMachineCode = GetStr(dict, "OriginMachineCode");
        if (local is not null && local.Status == "Draft"
            && !string.IsNullOrEmpty(local.OriginMachineCode)
            && local.OriginMachineCode != originMachineCode)
            return (false, $"Draft report {syncId} belongs to {local.OriginMachineCode}", false);

        if (local is null)
        {
            // Resolve PatientId from PatientSyncId.
            // If the Patient hasn't arrived yet, signal Reception to retry rather than storing PatientId=0.
            int patientId = 0;
            if (dict.TryGetValue("PatientSyncId", out var pElem) && pElem.ValueKind != JsonValueKind.Null)
            {
                var patientSyncId = Guid.Parse(pElem.GetString()!);
                patientId = db.Patients.Where(p => p.SyncId == patientSyncId).Select(p => p.Id).FirstOrDefault();
                if (patientId == 0)
                {
                    _logger.LogWarning("PatientSyncId {PSyncId} not found on Admin — deferring Report {SyncId}", patientSyncId, syncId);
                    return (false, "PatientNotFound", true); // retryable
                }
            }
            local = new SyncReport { SyncId = syncId, PatientId = patientId };
            db.Reports.Add(local);
        }

        local.ReportCode          = GetStr(dict, "ReportCode");
        local.TestDate            = GetDate(dict, "TestDate") ?? now;
        local.ReportDate          = GetDate(dict, "ReportDate") ?? now;
        local.Status              = GetStr(dict, "Status");
        local.BillingMode         = GetStr(dict, "BillingMode");
        local.PackageTotalPrice   = GetDecimal(dict, "PackageTotalPrice");
        local.PackageNameSnapshot = GetStrNullable(dict, "PackageNameSnapshot");
        local.PriceTierName       = GetStrNullable(dict, "PriceTierName");
        local.IsArchived          = GetBool(dict, "IsArchived");
        local.OriginMachineCode   = originMachineCode;
        local.UpdatedAt           = now;
        return (true, "ok", false);
    }

    private (bool Accepted, string Reason, bool Retryable) ApplyReportEntry(
        SyncDbContext db, PushEntryDto entry, Dictionary<string, JsonElement> dict, DateTime now)
    {
        var syncId = entry.EntitySyncId;
        var local  = db.ReportEntries.FirstOrDefault(e => e.SyncId == syncId);

        if (entry.Operation == "Delete")
        {
            if (local is not null) db.ReportEntries.Remove(local);
            return (true, "ok", false);
        }

        if (local is null)
        {
            // Resolve local FK IDs from SyncIds.
            int reportId = 0, testDefId = 0;
            if (dict.TryGetValue("ReportSyncId", out var rElem) && rElem.ValueKind != JsonValueKind.Null)
            {
                var rSyncId = Guid.Parse(rElem.GetString()!);
                reportId = db.Reports.Where(r => r.SyncId == rSyncId).Select(r => r.Id).FirstOrDefault();
                if (reportId == 0)
                {
                    _logger.LogWarning("ReportSyncId {RSyncId} not found on Admin — deferring ReportEntry {SyncId}", rSyncId, syncId);
                    return (false, "ReportNotFound", true); // retryable
                }
            }
            if (dict.TryGetValue("TestDefinitionSyncId", out var tElem) && tElem.ValueKind != JsonValueKind.Null)
            {
                var tSyncId = Guid.Parse(tElem.GetString()!);
                testDefId = db.TestDefinitions.Where(t => t.SyncId == tSyncId).Select(t => t.Id).FirstOrDefault();
            }
            local = new SyncReportEntry { SyncId = syncId, ReportId = reportId, TestDefinitionId = testDefId };
            db.ReportEntries.Add(local);
        }

        local.ResultValue        = GetStr(dict, "ResultValue");
        local.ResultFlag         = GetStr(dict, "ResultFlag");
        local.ReferenceRangeUsed = GetStr(dict, "ReferenceRangeUsed");
        local.PriceTierName      = GetStrNullable(dict, "PriceTierName");
        local.IsFromPackage      = GetBool(dict, "IsFromPackage");
        // Invariant: package entries must never carry an individual charged price.
        local.ChargedPrice       = local.IsFromPackage ? null : GetDecimal(dict, "ChargedPrice");
        local.UpdatedAt          = now;
        return (true, "ok", false);
    }

    private (bool Accepted, string Reason, bool Retryable) ApplyFinancialTransaction(
        SyncDbContext db, PushEntryDto entry, Dictionary<string, JsonElement> dict, DateTime now)
    {
        var syncId = entry.EntitySyncId;
        var local  = db.FinancialTransactions.FirstOrDefault(ft => ft.SyncId == syncId);

        if (entry.Operation == "Delete")
        {
            // Soft delete only — hard deletes are never applied on synced financial records.
            if (local is not null) { local.IsDeleted = true; local.DeletedAt = now; local.UpdatedAt = now; }
            return (true, "ok", false);
        }

        // Financial transactions are append-only on reception.
        if (entry.Operation == "Update" && local is not null)
            return (false, "Financial transactions are append-only — updates rejected", false);

        if (local is null)
        {
            local = new SyncFinancialTransaction { SyncId = syncId };
            db.FinancialTransactions.Add(local);
        }

        local.TransactionDate = GetDate(dict, "TransactionDate") ?? now;
        local.Type            = GetStr(dict, "Type");
        local.Category        = GetStr(dict, "Category");
        local.Description     = GetStr(dict, "Description");
        local.Amount          = GetDecimal(dict, "Amount") ?? 0m;
        local.Notes           = GetStrNullable(dict, "Notes");
        local.IsDeleted       = GetBool(dict, "IsDeleted");
        local.DeletedAt       = GetDate(dict, "DeletedAt");
        local.UpdatedAt       = now;
        return (true, "ok", false);
    }

    // ── JSON helpers ──────────────────────────────────────────────────────────

    private static Dictionary<string, JsonElement> Deserialize(string json)
        => JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json)
           ?? new Dictionary<string, JsonElement>();

    private static string GetStr(Dictionary<string, JsonElement> d, string key)
        => d.TryGetValue(key, out var v) ? v.GetString() ?? string.Empty : string.Empty;

    private static string? GetStrNullable(Dictionary<string, JsonElement> d, string key)
        => d.TryGetValue(key, out var v) && v.ValueKind != JsonValueKind.Null ? v.GetString() : null;

    private static int GetInt(Dictionary<string, JsonElement> d, string key)
        => d.TryGetValue(key, out var v) ? v.GetInt32() : 0;

    private static bool GetBool(Dictionary<string, JsonElement> d, string key)
        => d.TryGetValue(key, out var v) && v.GetBoolean();

    private static decimal? GetDecimal(Dictionary<string, JsonElement> d, string key)
    {
        if (!d.TryGetValue(key, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        if (v.ValueKind == JsonValueKind.String && decimal.TryParse(v.GetString(), out var sd)) return sd;
        if (v.ValueKind == JsonValueKind.Number && v.TryGetDecimal(out var nd)) return nd;
        return null;
    }

    private static DateTime? GetDate(Dictionary<string, JsonElement> d, string key)
    {
        if (!d.TryGetValue(key, out var v) || v.ValueKind == JsonValueKind.Null) return null;
        return DateTime.TryParse(v.GetString(), null, System.Globalization.DateTimeStyles.RoundtripKind, out var dt) ? dt : null;
    }
}

// ── Push DTO ──────────────────────────────────────────────────────────────────

public class PushEntryDto
{
    public Guid   CorrelationId     { get; set; }
    public string EntityType        { get; set; } = string.Empty;
    public Guid   EntitySyncId      { get; set; }
    public string Operation         { get; set; } = string.Empty;
    public string Payload           { get; set; } = string.Empty;
    public string SourceMachineCode { get; set; } = string.Empty;
}
