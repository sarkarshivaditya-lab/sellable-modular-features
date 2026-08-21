namespace UrbanDiagnosticCentre.Models;

/// <summary>
/// One row per database. Identifies the machine and its role in the sync topology.
/// MachineCode = "ADM" (authoritative) or "RCP" (replica).
/// </summary>
public class SyncIdentity
{
    public int    Id              { get; set; } = 1;
    public Guid   MachineId      { get; set; } = Guid.NewGuid();
    public string MachineCode    { get; set; } = "ADM";
    public DateTime CreatedAt    { get; set; } = DateTime.UtcNow;

    // Set on Reception only — the base URL of the Admin's SyncService.
    public string? AdminApiBaseUrl { get; set; }

    // Pre-shared API key used to authenticate to the Admin SyncService.
    public string? ApiKey { get; set; }
}
