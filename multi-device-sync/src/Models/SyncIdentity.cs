namespace UrbanDiagnosticCentre.Models;

public class SyncIdentity
{
    public int Id { get; set; } = 1;
    public Guid MachineId { get; set; } = Guid.NewGuid();
    public string MachineCode { get; set; } = "ADM";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? AdminApiBaseUrl { get; set; }
    public string? ApiKey { get; set; }
}
