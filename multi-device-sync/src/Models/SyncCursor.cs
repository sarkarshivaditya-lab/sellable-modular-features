namespace UrbanDiagnosticCentre.Models;

public class SyncCursor
{
    public int Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public DateTime LastPulledAt { get; set; } = DateTime.MinValue;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
