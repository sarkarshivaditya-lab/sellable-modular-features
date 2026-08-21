namespace UrbanDiagnosticCentre.Models;

public enum Gender { Male, Female }

public class Patient
{
    public int Id { get; set; }
    public string FullName { get; set; } = string.Empty;
    public int Age { get; set; }
    public Gender Gender { get; set; }
    public string PhoneNumber { get; set; } = string.Empty;
    public string ReferringDoctor { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public bool IsArchived { get; set; } = false;
    public DateTime? ArchivedAt { get; set; }
    public int? CreatedByUserId { get; set; }

    // Sync
    public Guid SyncId { get; set; } = Guid.NewGuid();
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User? CreatedByUser { get; set; }
    public List<Report> Reports { get; set; } = new();
}
