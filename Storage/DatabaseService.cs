using System.Globalization;
using System.IO;
using Microsoft.Data.Sqlite;
using S7TrendMonitor.Models;

namespace S7TrendMonitor.Storage;

/// <summary>
/// 基于 SQLite 的本地数据存储服务，负责变量配置、采样数据与应用设置的持久化。
/// 表结构由 <see cref="DatabaseInitializer"/> 创建，本类仅负责读写操作。
/// </summary>
public class DatabaseService
{
    private readonly string _connectionString;
    private readonly object _lock = new();

    public DatabaseService(string dbPath)
    {
        if (string.IsNullOrWhiteSpace(dbPath))
            throw new ArgumentNullException(nameof(dbPath));

        var dir = Path.GetDirectoryName(dbPath);
        if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            Directory.CreateDirectory(dir);

        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared
        }.ToString();
    }

    /// <summary>初始化数据库（兼容方法，实际表结构由 DatabaseInitializer 创建）。</summary>
    public Task InitializeAsync()
    {
        return Task.CompletedTask;
    }

    private SqliteConnection OpenConnection()
    {
        var conn = new SqliteConnection(_connectionString);
        conn.Open();
        return conn;
    }

    /// <summary>写入一批采样数据（每个变量一行，读取错误的变量跳过）。</summary>
    public Task InsertSamplesAsync(SampleBatch batch)
    {
        if (batch is null) throw new ArgumentNullException(nameof(batch));

        long ts = batch.TimestampMs;

        return Task.Run(() =>
        {
            lock (_lock)
            {
                using var conn = OpenConnection();
                using var tx = conn.BeginTransaction();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "INSERT INTO samples (variable_id, timestamp_ms, value) VALUES (@vid, @ts, @val);";

                foreach (var kv in batch.Values)
                {
                    if (batch.ReadErrors.TryGetValue(kv.Key, out bool err) && err) continue;

                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@vid", kv.Key);
                    cmd.Parameters.AddWithValue("@ts", ts);
                    cmd.Parameters.AddWithValue("@val", kv.Value);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
            }
        });
    }

    /// <summary>读取全部变量配置（按 sort_order 排序）。</summary>
    public Task<List<VariableConfig>> GetVariablesAsync()
    {
        return Task.Run(() =>
        {
            var list = new List<VariableConfig>();

            lock (_lock)
            {
                using var conn = OpenConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "SELECT id, address, display_name, data_type, scale_min, scale_max, color_hex, is_enabled, sort_order, created_at " +
                    "FROM variables ORDER BY sort_order, id;";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new VariableConfig
                    {
                        Id = reader.GetInt32(0),
                        Address = reader.GetString(1),
                        DisplayName = reader.GetString(2),
                        DataType = reader.GetString(3),
                        ScaleMin = reader.IsDBNull(4) ? 0 : reader.GetDouble(4),
                        ScaleMax = reader.IsDBNull(5) ? 0 : reader.GetDouble(5),
                        ColorHex = reader.IsDBNull(6) ? "#1E90FF" : reader.GetString(6),
                        IsEnabled = reader.GetInt32(7) != 0,
                        SortOrder = reader.GetInt32(8),
                        CreatedAt = DateTime.TryParse(reader.GetString(9), null, DateTimeStyles.RoundtripKind, out var dt) ? dt : DateTime.Now
                    });
                }
            }

            return list;
        });
    }

    /// <summary>新增或更新变量配置。</summary>
    /// <remarks>
    /// 当 Id==0 时为新增，不指定主键，由 SQLite AUTOINCREMENT 自动分配；
    /// 当 Id>0 时为编辑，使用 UPSERT 更新已有记录。
    /// </remarks>
    public Task SaveVariableAsync(VariableConfig cfg)
    {
        if (cfg is null) throw new ArgumentNullException(nameof(cfg));

        return Task.Run(() =>
        {
            lock (_lock)
            {
                using var conn = OpenConnection();

                if (cfg.Id == 0)
                {
                    // 新增：不指定 id，让数据库自增
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        INSERT INTO variables (address, display_name, data_type, scale_min, scale_max, color_hex, is_enabled, sort_order, created_at)
                        VALUES (@addr, @name, @dt, @smin, @smax, @color, @en, @order, @created);
                    ";
                    cmd.Parameters.AddWithValue("@addr", cfg.Address);
                    cmd.Parameters.AddWithValue("@name", cfg.DisplayName);
                    cmd.Parameters.AddWithValue("@dt", cfg.DataType);
                    cmd.Parameters.AddWithValue("@smin", cfg.ScaleMin);
                    cmd.Parameters.AddWithValue("@smax", cfg.ScaleMax);
                    cmd.Parameters.AddWithValue("@color", cfg.ColorHex);
                    cmd.Parameters.AddWithValue("@en", cfg.IsEnabled ? 1 : 0);
                    cmd.Parameters.AddWithValue("@order", cfg.SortOrder);
                    cmd.Parameters.AddWithValue("@created", cfg.CreatedAt.ToString("O"));
                    cmd.ExecuteNonQuery();

                    // 取回自增的 Id
                    using var idCmd = conn.CreateCommand();
                    idCmd.CommandText = "SELECT last_insert_rowid();";
                    cfg.Id = Convert.ToInt32(idCmd.ExecuteScalar());
                }
                else
                {
                    // 编辑：UPSERT 更新已有记录
                    using var cmd = conn.CreateCommand();
                    cmd.CommandText = @"
                        INSERT INTO variables (id, address, display_name, data_type, scale_min, scale_max, color_hex, is_enabled, sort_order, created_at)
                        VALUES (@id, @addr, @name, @dt, @smin, @smax, @color, @en, @order, @created)
                        ON CONFLICT(id) DO UPDATE SET
                            address=excluded.address,
                            display_name=excluded.display_name,
                            data_type=excluded.data_type,
                            scale_min=excluded.scale_min,
                            scale_max=excluded.scale_max,
                            color_hex=excluded.color_hex,
                            is_enabled=excluded.is_enabled,
                            sort_order=excluded.sort_order,
                            created_at=excluded.created_at;
                    ";
                    cmd.Parameters.AddWithValue("@id", cfg.Id);
                    cmd.Parameters.AddWithValue("@addr", cfg.Address);
                    cmd.Parameters.AddWithValue("@name", cfg.DisplayName);
                    cmd.Parameters.AddWithValue("@dt", cfg.DataType);
                    cmd.Parameters.AddWithValue("@smin", cfg.ScaleMin);
                    cmd.Parameters.AddWithValue("@smax", cfg.ScaleMax);
                    cmd.Parameters.AddWithValue("@color", cfg.ColorHex);
                    cmd.Parameters.AddWithValue("@en", cfg.IsEnabled ? 1 : 0);
                    cmd.Parameters.AddWithValue("@order", cfg.SortOrder);
                    cmd.Parameters.AddWithValue("@created", cfg.CreatedAt.ToString("O"));
                    cmd.ExecuteNonQuery();
                }
            }
        });
    }

    /// <summary>删除变量配置及其历史采样数据。</summary>
    public Task DeleteVariableAsync(int id)
    {
        return Task.Run(() =>
        {
            lock (_lock)
            {
                using var conn = OpenConnection();
                using var tx = conn.BeginTransaction();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM variables WHERE id=@id;";
                cmd.Parameters.AddWithValue("@id", id);
                cmd.ExecuteNonQuery();

                cmd.CommandText = "DELETE FROM samples WHERE variable_id=@id2;";
                cmd.Parameters.Clear();
                cmd.Parameters.AddWithValue("@id2", id);
                cmd.ExecuteNonQuery();

                tx.Commit();
            }
        });
    }

    /// <summary>删除早于指定时间戳的采样数据，返回删除行数。</summary>
    public Task<int> DeleteSamplesBeforeAsync(long beforeTimestampMs)
    {
        return Task.Run(() =>
        {
            lock (_lock)
            {
                using var conn = OpenConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "DELETE FROM samples WHERE timestamp_ms < @ts;";
                cmd.Parameters.AddWithValue("@ts", beforeTimestampMs);
                return cmd.ExecuteNonQuery();
            }
        });
    }

    /// <summary>读取应用设置（不存在时返回默认值）。</summary>
    /// <remarks>app_settings 表为 key-value 结构，每项设置单独一行。</remarks>
    public Task<AppSettings> GetSettingsAsync()
    {
        return Task.Run(() =>
        {
            var settings = new AppSettings();

            lock (_lock)
            {
                using var conn = OpenConnection();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT key, value FROM app_settings;";

                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string key = reader.GetString(0);
                    string? value = reader.IsDBNull(1) ? null : reader.GetString(1);

                    ApplySetting(settings, key, value);
                }
            }

            return settings;
        });
    }

    /// <summary>保存应用设置（key-value 方式写入）。</summary>
    public Task SaveSettingsAsync(AppSettings settings)
    {
        if (settings is null) throw new ArgumentNullException(nameof(settings));

        return Task.Run(() =>
        {
            lock (_lock)
            {
                using var conn = OpenConnection();
                using var tx = conn.BeginTransaction();
                using var cmd = conn.CreateCommand();
                cmd.CommandText =
                    "INSERT INTO app_settings (key, value) VALUES (@k, @v) " +
                    "ON CONFLICT(key) DO UPDATE SET value=excluded.value;";

                var entries = new (string key, string value)[]
                {
                    ("SamplingIntervalMs", settings.SamplingIntervalMs.ToString(CultureInfo.InvariantCulture)),
                    ("DataRetentionHours", settings.DataRetentionHours.ToString(CultureInfo.InvariantCulture)),
                    ("ChartWindowSeconds", settings.ChartWindowSeconds.ToString(CultureInfo.InvariantCulture)),
                    ("AutoStartSampling", settings.AutoStartSampling ? "1" : "0"),
                    ("PauseChartWhenFull", settings.PauseChartWhenFull ? "1" : "0"),
                    ("LastConnectionType", settings.LastConnectionType)
                };

                foreach (var (key, value) in entries)
                {
                    cmd.Parameters.Clear();
                    cmd.Parameters.AddWithValue("@k", key);
                    cmd.Parameters.AddWithValue("@v", value);
                    cmd.ExecuteNonQuery();
                }

                tx.Commit();
            }
        });
    }

    /// <summary>将 key-value 中的单个设置应用到 AppSettings 对象。</summary>
    private static void ApplySetting(AppSettings settings, string key, string? value)
    {
        if (string.IsNullOrEmpty(value)) return;

        switch (key)
        {
            case "SamplingIntervalMs":
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var si))
                    settings.SamplingIntervalMs = si;
                break;
            case "DataRetentionHours":
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var dr))
                    settings.DataRetentionHours = dr;
                break;
            case "ChartWindowSeconds":
                if (int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var cw))
                    settings.ChartWindowSeconds = cw;
                break;
            case "AutoStartSampling":
                settings.AutoStartSampling = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
                break;
            case "PauseChartWhenFull":
                settings.PauseChartWhenFull = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
                break;
            case "LastConnectionType":
                settings.LastConnectionType = value;
                break;
        }
    }
}
