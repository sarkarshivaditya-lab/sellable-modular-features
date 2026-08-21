using System.Diagnostics;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using UrbanDiagnosticCentre.Data;
using UrbanDiagnosticCentre.Helpers;
using UrbanDiagnosticCentre.Models;

namespace UrbanDiagnosticCentre.Services;

public class BackupService
{
    private readonly AppDbContext _db;
    private readonly SettingsService _settingsService;

    private static readonly string AppDataFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "UrbanDiagnosticCentre");

    private static string DbPath => Path.Combine(AppDataFolder, "udc.db");

    private string ReportsRoot
    {
        get
        {
            var s = _settingsService.Load();
            return !string.IsNullOrEmpty(s.ReportsRootPath)
                ? s.ReportsRootPath
                : Path.Combine(AppDataFolder, "Reports");
        }
    }

    public BackupService(AppDbContext db, SettingsService settingsService)
    {
        _db = db;
        _settingsService = settingsService;
    }

    public string GetDefaultBackupFolder()
    {
        var s = _settingsService.Load();
        return !string.IsNullOrEmpty(s.BackupsRootPath)
            ? s.BackupsRootPath
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "UDC Backups");
    }

    public string CreateBackup(string destinationFolder, string note = "", bool isAutoBackup = false)
    {
        Directory.CreateDirectory(destinationFolder);
        CheckDiskSpace(destinationFolder);
        try { _db.Database.ExecuteSqlRaw("PRAGMA wal_checkpoint(FULL);"); } catch { }

        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var zipPath = Path.Combine(destinationFolder, $"UDC_Backup_{timestamp}.zip");
        int pdfCount = 0;

        using (var zip = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            if (File.Exists(DbPath))
                zip.CreateEntryFromFile(DbPath, "udc.db", CompressionLevel.Fastest);

            if (Directory.Exists(ReportsRoot))
            {
                foreach (var file in Directory.GetFiles(ReportsRoot, "*.pdf", SearchOption.AllDirectories))
                {
                    var relative = "Reports/" + Path.GetRelativePath(ReportsRoot, file).Replace('\\', '/');
                    zip.CreateEntryFromFile(file, relative, CompressionLevel.Fastest);
                    pdfCount++;
                }
            }

            var settings = _settingsService.Load();
            var manifest = new
            {
                CreatedAt = DateTime.Now.ToString("O"),
                AppVersion = AppInfo.Version,
                CentreName = settings.CentreName,
                PdfCount = pdfCount,
                Note = note,
                IsAutoBackup = isAutoBackup
            };
            var manifestEntry = zip.CreateEntry("manifest.json", CompressionLevel.NoCompression);
            using var writer = new StreamWriter(manifestEntry.Open());
            writer.Write(JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true }));
        }

        var sizeBytes = new FileInfo(zipPath).Length;
        _db.BackupRecords.Add(new BackupRecord
        {
            BackupDate = DateTime.Now,
            BackupPath = zipPath,
            BackupSizeBytes = sizeBytes,
            Note = note,
            IsAutoBackup = isAutoBackup
        });
        _db.SaveChanges();
        return zipPath;
    }

    public string RestoreBackup(string zipPath, string safetyBackupFolder)
    {
        if (!File.Exists(zipPath)) throw new FileNotFoundException("Backup file not found.", zipPath);

        try
        {
            using var probe = ZipFile.OpenRead(zipPath);
            if (probe.GetEntry("udc.db") is null) throw new InvalidDataException();
        }
        catch (InvalidDataException)
        {
            throw new InvalidDataException("Invalid backup: the selected file is not a valid UDC backup archive.");
        }
        catch (Exception)
        {
            throw new InvalidDataException("The selected file could not be read. It may be corrupted or not a ZIP archive.");
        }

        var safetyPath = CreateBackup(safetyBackupFolder,
            note: "Pre-restore safety backup", isAutoBackup: true);
        var tempDir = Path.Combine(Path.GetTempPath(), $"UDC_Restore_{DateTime.Now.Ticks}");
        Directory.CreateDirectory(tempDir);

        try
        {
            ZipFile.ExtractToDirectory(zipPath, tempDir, overwriteFiles: true);
            _db.Database.CloseConnection();
            SqliteConnection.ClearAllPools();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            File.Copy(Path.Combine(tempDir, "udc.db"), DbPath, overwrite: true);

            var reportsSource = Path.Combine(tempDir, "Reports");
            if (Directory.Exists(reportsSource))
            {
                if (Directory.Exists(ReportsRoot)) Directory.Delete(ReportsRoot, recursive: true);
                CopyDirectory(reportsSource, ReportsRoot);
            }
        }
        finally
        {
            try { Directory.Delete(tempDir, recursive: true); } catch { }
        }

        return safetyPath;
    }

    public List<BackupRecord> GetRecentBackups(int count = 20) =>
        _db.BackupRecords.OrderByDescending(b => b.BackupDate).Take(count).ToList();

    public List<BackupRecord> GetBackupsOlderThan(DateTime cutoff) =>
        _db.BackupRecords.Where(b => !b.IsAutoBackup && b.BackupDate < cutoff).OrderBy(b => b.BackupDate).ToList();

    public void OpenFolder(string folderPath)
    {
        if (!Directory.Exists(folderPath)) Directory.CreateDirectory(folderPath);
        Process.Start(new ProcessStartInfo { FileName = folderPath, UseShellExecute = true });
    }

    private void CheckDiskSpace(string destinationFolder)
    {
        var root = Path.GetPathRoot(Path.GetFullPath(destinationFolder));
        if (root is null) return;
        var reportsRoot = ReportsRoot;
        long estimated = 0;
        if (File.Exists(DbPath)) estimated += new FileInfo(DbPath).Length;
        if (Directory.Exists(reportsRoot))
            estimated += Directory.GetFiles(reportsRoot, "*.pdf", SearchOption.AllDirectories)
                                  .Sum(f => new FileInfo(f).Length);
        var drive = new DriveInfo(root);
        if (drive.IsReady && drive.AvailableFreeSpace < (long)(estimated * 1.5))
            throw new IOException("Not enough disk space on the selected drive to create the backup.");
    }

    private static void CopyDirectory(string source, string destination)
    {
        Directory.CreateDirectory(destination);
        foreach (var file in Directory.GetFiles(source))
            File.Copy(file, Path.Combine(destination, Path.GetFileName(file)), overwrite: true);
        foreach (var dir in Directory.GetDirectories(source))
            CopyDirectory(dir, Path.Combine(destination, Path.GetFileName(dir)));
    }
}
