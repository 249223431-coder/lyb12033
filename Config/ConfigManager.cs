using System.Text.Json;
using S7TrendMonitor.Models;

namespace S7TrendMonitor.Config;

/// <summary>
/// 配置管理器，负责加载和保存连接配置与应用设置
/// </summary>
public class ConfigManager
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    /// <summary>
    /// 从connection.json加载连接配置，文件不存在返回默认值
    /// </summary>
    public ConnectionConfig LoadConnectionConfig()
    {
        var path = ConfigPaths.ConnectionConfigPath;
        if (!File.Exists(path))
        {
            return new ConnectionConfig();
        }
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ConnectionConfig>(json, JsonOptions) ?? new ConnectionConfig();
    }

    /// <summary>
    /// 保存连接配置到connection.json
    /// </summary>
    public void SaveConnectionConfig(ConnectionConfig config)
    {
        var path = ConfigPaths.ConnectionConfigPath;
        var json = JsonSerializer.Serialize(config, JsonOptions);
        File.WriteAllText(path, json);
    }

    /// <summary>
    /// 从settings.json加载应用设置，文件不存在返回默认值
    /// </summary>
    public AppSettings LoadAppSettings()
    {
        var path = ConfigPaths.AppSettingsPath;
        if (!File.Exists(path))
        {
            return new AppSettings();
        }
        var json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
    }

    /// <summary>
    /// 保存应用设置到settings.json
    /// </summary>
    public void SaveAppSettings(AppSettings settings)
    {
        var path = ConfigPaths.AppSettingsPath;
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(path, json);
    }
}
