namespace UrbanDiagnosticCentre.Models;

public enum BillingMode { Normal, Package }

public class Report
{
    public int Id { get; set; }
    public string ReportCode { get; set; } = string.Empty;
    public int PatientId { get; set; }
    public DateTime TestDate { get; set; }
    public DateTime ReportDate { get; set; }
    public string? PdfPath { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsArchived { get; set; } = false;
    public DateTime? ArchivedAt { get; set; }
    public ReportStatus Status { get; set; } = ReportStatus.Draft;
    public int? CreatedByUserId { get; set; }
    public int? ModifiedByUserId { get; set; }
    public DateTime? ModifiedAt { get; set; }
    public DateTime? ReportGeneratedAt { get; set; }
    public DateTime? PrintedAt { get; set; }
    public int PdfVersion { get; set; } = 0;
    public string? PriceTierName { get; set; }

    // Package billing — non-null when the report is sold as a fixed-price bundle.
    // Snapshots are captured at apply time; immune to later package edits or deletion.
    public BillingMode BillingMode        { get; set; } = BillingMode.Normal;
    public string?     PackageNameSnapshot { get; set; }
    public decimal?    PackageTotalPrice   { get; set; }

    // Sync
    public Guid SyncId { get; set; } = Guid.NewGuid();
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string OriginMachineCode { get; set; } = string.Empty;

    public Patient Patient { get; set; } = null!;
    public User? CreatedByUser { get; set; }
    public User? ModifiedByUser { get; set; }
    public List<ReportEntry> Entries { get; set; } = new();
}
