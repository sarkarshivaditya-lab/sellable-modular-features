namespace UrbanDiagnosticCentre.Models;

public class BackupRecord
{
    public int Id { get; set; }
    public DateTime BackupDate { get; set; } = DateTime.Now;
    public string BackupPath { get; set; } = string.Empty;
    public long BackupSizeBytes { get; set; }
    public string Note { get; set; } = string.Empty;
    public bool IsAutoBackup { get; set; }

    public string SizeDisplay => BackupSizeBytes < 1_048_576
        ? $"{BackupSizeBytes / 1024.0:F1} KB"
        : $"{BackupSizeBytes / 1_048_576.0:F1} MB";

    public string TypeDisplay => IsAutoBackup ? "Auto" : "Manual";
}
