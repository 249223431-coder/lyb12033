namespace S7TrendMonitor.Config;

public static class ConfigPaths
{
    public static string AppDataDir =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "S7TrendMonitor");

    public static string DatabasePath => Path.Combine(AppDataDir, "trend_data.db");
    public static string LogDir => Path.Combine(AppDataDir, "logs");
    public static string ConnectionConfigPath => Path.Combine(AppDataDir, "connection.json");
    public static string AppSettingsPath => Path.Combine(AppDataDir, "settings.json");

    public static void EnsureDirectories()
    {
        Directory.CreateDirectory(AppDataDir);
        Directory.CreateDirectory(LogDir);
    }
}
