namespace UrbanDiagnosticCentre.Models;

public class AppSyncLogEntry
{
    public int Id { get; set; }
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = string.Empty;
    public Guid EntitySyncId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public string SourceMachineCode { get; set; } = string.Empty;
    public DateTime AppliedAt { get; set; } = DateTime.UtcNow;
}
