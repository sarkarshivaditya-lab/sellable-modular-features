namespace UrbanDiagnosticCentre.Models;

public class TestDefinition
{
    public int Id { get; set; }
    public string TestName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string SampleType { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;

    public decimal MaleMinValue { get; set; }
    public decimal MaleMaxValue { get; set; }

    public decimal FemaleMinValue { get; set; }
    public decimal FemaleMaxValue { get; set; }

    public decimal ChildMinValue { get; set; }
    public decimal ChildMaxValue { get; set; }

    public int DecimalPrecision { get; set; } = 2;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsArchived { get; set; } = false;
    public DateTime? ArchivedAt { get; set; }

    // Sync
    public Guid SyncId { get; set; } = Guid.NewGuid();
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
