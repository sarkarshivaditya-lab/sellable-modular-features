namespace UrbanDiagnosticCentre.Models;

/// <summary>
/// Admin-side append-only changelog. Every entity mutation on Admin is logged here
/// so Reception can incrementally pull changes. Also records mutations applied from
/// Reception pushes (so other Reception machines can learn about them too).
/// </summary>
public class AppSyncLogEntry
{
    public int      Id                 { get; set; }
    public Guid     CorrelationId      { get; set; } = Guid.NewGuid();
    public string   EntityType         { get; set; } = string.Empty;
    public Guid     EntitySyncId       { get; set; }
    public string   Operation          { get; set; } = string.Empty; // "Insert" | "Update" | "Delete"
    public string   Payload            { get; set; } = string.Empty; // JSON — full entity snapshot
    public string   SourceMachineCode  { get; set; } = string.Empty;
    public DateTime AppliedAt          { get; set; } = DateTime.UtcNow;
}
