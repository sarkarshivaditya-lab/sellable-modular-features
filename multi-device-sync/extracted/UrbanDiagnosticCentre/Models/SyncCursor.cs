namespace UrbanDiagnosticCentre.Models;

/// <summary>
/// Tracks the high-water mark of what has been pulled from Admin.
/// One row per entity type on the Reception machine.
/// </summary>
public class SyncCursor
{
    public int      Id            { get; set; }
    public string   EntityType    { get; set; } = string.Empty;
    public DateTime LastPulledAt  { get; set; } = DateTime.MinValue;
    public DateTime UpdatedAt     { get; set; } = DateTime.UtcNow;
}
