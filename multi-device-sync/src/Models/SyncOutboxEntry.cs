namespace UrbanDiagnosticCentre.Models;

public class SyncOutboxEntry
{
    public int Id { get; set; }
    public Guid CorrelationId { get; set; } = Guid.NewGuid();
    public string EntityType { get; set; } = string.Empty;
    public Guid EntitySyncId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsSent { get; set; }
    public DateTime? SentAt { get; set; }
    public int AttemptCount { get; set; }
    public string? LastError { get; set; }
}
