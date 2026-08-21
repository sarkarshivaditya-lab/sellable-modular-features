using System.IO;

namespace UrbanDiagnosticCentre.Services;

public static class ErrorLoggingService
{
    private static readonly string _logFolder =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                     "UrbanDiagnosticCentre", "Logs");

    public static string LogFolder => _logFolder;

    public static void Log(Exception ex, string context = "")
    {
        var lines = new List<string>
        {
            $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [ERROR] [{context}] {ex.GetType().Name}: {ex.Message}",
            ex.StackTrace ?? string.Empty
        };

        if (ex.InnerException is not null)
            lines.Add($"Inner: {ex.InnerException.GetType().Name}: {ex.InnerException.Message}");

        lines.Add("---");
        WriteLines(lines);
    }

    public static void LogInfo(string message) =>
        WriteLines([$"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [INFO] {message}"]);

    // Deletes log files older than retentionDays. Call once at startup.
    public static void PurgeOldLogs(int retentionDays = 90)
    {
        try
        {
            if (!Directory.Exists(_logFolder)) return;
            var cutoff = DateTime.Now.AddDays(-retentionDays);
            foreach (var file in Directory.GetFiles(_logFolder, "*.log"))
            {
                if (File.GetLastWriteTime(file) < cutoff)
                    File.Delete(file);
            }
        }
        catch { /* must never throw from the logger */ }
    }

    private static void WriteLines(IEnumerable<string> lines)
    {
        try
        {
            Directory.CreateDirectory(_logFolder);
            var file = Path.Combine(_logFolder, $"{DateTime.Now:yyyy-MM-dd}.log");
            File.AppendAllText(file, string.Join(Environment.NewLine, lines) + Environment.NewLine);
        }
        catch { /* must never throw from the logger */ }
    }
}
