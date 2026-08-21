namespace UrbanDiagnosticCentre.Models;

public class TestPackageItem
{
    public int Id { get; set; }
    public int PackageId { get; set; }
    public int TestDefinitionId { get; set; }
    public int SortOrder { get; set; }

    // Sync
    public Guid SyncId { get; set; } = Guid.NewGuid();
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public TestPackage Package { get; set; } = null!;
    public TestDefinition TestDefinition { get; set; } = null!;
}
