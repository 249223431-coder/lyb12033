using System.IO;

namespace S7TrendMonitor.Utils;

public static class Logger
{
    private static readonly object _lock = new();
    private static string _logDir = "";

    public static void Init(string logDir)
    {
        _logDir = logDir;
        if (!Directory.Exists(_logDir))
            Directory.CreateDirectory(_logDir);
    }

    public static void Info(string message) => WriteLog("INFO", message);
    public static void Warning(string message) => WriteLog("WARN", message);
    public static void Error(string message) => WriteLog("ERROR", message);
    public static void Error(string message, Exception ex) => WriteLog("ERROR", $"{message}: {ex}");

    private static void WriteLog(string level, string message)
    {
        try
        {
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var line = $"[{timestamp}] [{level}] {message}{Environment.NewLine}";
            var fileName = $"log_{DateTime.Now:yyyyMMdd}.txt";
            var filePath = Path.Combine(_logDir, fileName);
            lock (_lock)
            {
                File.AppendAllText(filePath, line);
            }
        }
        catch { }
    }
}
