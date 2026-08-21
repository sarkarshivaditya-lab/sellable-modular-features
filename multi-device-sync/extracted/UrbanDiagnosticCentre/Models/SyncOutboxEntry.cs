namespace UrbanDiagnosticCentre.Models;

/// <summary>
/// Reception-side queue. One row per entity change that must be pushed to Admin.
/// Accumulated offline, flushed when connectivity is restored.
/// </summary>
public class SyncOutboxEntry
{
    public int      Id             { get; set; }
    public Guid     CorrelationId  { get; set; } = Guid.NewGuid();
    public string   EntityType     { get; set; } = string.Empty;
    public Guid     EntitySyncId   { get; set; }
    public string   Operation      { get; set; } = string.Empty; // "Insert" | "Update" | "Delete"
    public string   Payload        { get; set; } = string.Empty; // JSON
    public DateTime CreatedAt      { get; set; } = DateTime.UtcNow;
    public bool     IsSent         { get; set; } = false;
    public DateTime? SentAt        { get; set; }
    public int      AttemptCount   { get; set; } = 0;
    public string?  LastError      { get; set; }
}
