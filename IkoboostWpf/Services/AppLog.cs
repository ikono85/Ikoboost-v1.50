using System.IO;

namespace IkoboostWpf.Services;

public static class AppLog
{
    private static readonly object Sync = new();

    public static void Error(string area, Exception ex) =>
        Write("ERROR", area, ex.Message);

    public static void Warning(string area, string message) =>
        Write("WARN", area, message);

    private static void Write(string level, string area, string message)
    {
        try
        {
            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Ikoboost");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, "ikoboost.log");
            var line = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss} [{level}] {area}: {message}{Environment.NewLine}";
            lock (Sync)
                File.AppendAllText(path, line);
        }
        catch
        {
            // Logging must never break the application.
        }
    }
}
