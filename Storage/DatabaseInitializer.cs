using Microsoft.Data.Sqlite;

namespace S7TrendMonitor.Storage;

/// <summary>
/// 数据库初始化器，负责创建表结构和索引
/// </summary>
public class DatabaseInitializer
{
    private readonly string _dbPath;

    public DatabaseInitializer(string dbPath)
    {
        _dbPath = dbPath;
    }

    /// <summary>
    /// 初始化数据库，创建所有表和索引（如果不存在）
    /// </summary>
    public void Init()
    {
        using var connection = new SqliteConnection($"Data Source={_dbPath}");
        connection.Open();

        // 启用WAL模式，提升并发读写性能
        using (var pragmaCmd = connection.CreateCommand())
        {
            pragmaCmd.CommandText = "PRAGMA journal_mode=WAL;";
            pragmaCmd.ExecuteNonQuery();
        }

        // 启用外键约束
        using (var pragmaCmd = connection.CreateCommand())
        {
            pragmaCmd.CommandText = "PRAGMA foreign_keys=ON;";
            pragmaCmd.ExecuteNonQuery();
        }

        // 创建variables表
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS variables (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    address TEXT NOT NULL UNIQUE,
                    display_name TEXT NOT NULL,
                    data_type TEXT NOT NULL,
                    scale_min REAL,
                    scale_max REAL,
                    color_hex TEXT,
                    is_enabled INTEGER DEFAULT 1,
                    sort_order INTEGER DEFAULT 0,
                    created_at TEXT DEFAULT (datetime('now','localtime'))
                );
                """;
            cmd.ExecuteNonQuery();
        }

        // 创建samples表
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS samples (
                    id INTEGER PRIMARY KEY AUTOINCREMENT,
                    variable_id INTEGER NOT NULL,
                    timestamp_ms INTEGER NOT NULL,
                    value REAL NOT NULL,
                    FOREIGN KEY (variable_id) REFERENCES variables(id) ON DELETE CASCADE
                );
                """;
            cmd.ExecuteNonQuery();
        }

        // 创建索引：按变量ID和时间戳降序查询（用于图表绘制）
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_samples_var_time ON samples(variable_id, timestamp_ms DESC);";
            cmd.ExecuteNonQuery();
        }

        // 创建索引：按时间戳查询（用于数据保留清理）
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_samples_timestamp ON samples(timestamp_ms);";
            cmd.ExecuteNonQuery();
        }

        // 创建app_settings表
        using (var cmd = connection.CreateCommand())
        {
            cmd.CommandText = """
                CREATE TABLE IF NOT EXISTS app_settings (
                    key TEXT PRIMARY KEY,
                    value TEXT
                );
                """;
            cmd.ExecuteNonQuery();
        }
    }
}
