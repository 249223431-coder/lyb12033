using System.Reflection;
using System.Runtime.InteropServices;
using S7TrendMonitor.Forms;

namespace S7TrendMonitor;

static class Program
{
    private const string LibnodaveResourceName = "libnodave.dll";
    private static string? _extractedDllPath;

    [STAThread]
    static void Main()
    {
        // 注册 DLL 导入解析器，从嵌入资源动态加载 libnodave.dll
        NativeLibrary.SetDllImportResolver(Assembly.GetExecutingAssembly(), ResolveLibnodave);

        ApplicationConfiguration.Initialize();
        Application.Run(new MainForm());
    }

    /// <summary>
    /// DllImport 解析器：当 P/Invoke 需要加载 libnodave.dll 时，
    /// 从嵌入资源中提取到临时文件并加载，支持单文件部署。
    /// </summary>
    private static IntPtr ResolveLibnodave(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!string.Equals(libraryName, "libnodave.dll", StringComparison.OrdinalIgnoreCase))
            return IntPtr.Zero;

        // 如果已经提取过，直接加载
        if (_extractedDllPath != null && File.Exists(_extractedDllPath))
        {
            return NativeLibrary.Load(_extractedDllPath);
        }

        // 从嵌入资源中读取 libnodave.dll
        var resourceName = LibnodaveResourceName;
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream == null)
            return IntPtr.Zero;

        // 提取到临时目录（使用进程ID避免多实例冲突）
        var tempDir = Path.Combine(Path.GetTempPath(), "S7TrendMonitor");
        Directory.CreateDirectory(tempDir);
        _extractedDllPath = Path.Combine(tempDir, $"libnodave_{Environment.ProcessId}.dll");

        using var fileStream = new FileStream(_extractedDllPath, FileMode.Create, FileAccess.Write, FileShare.None);
        stream.CopyTo(fileStream);
        fileStream.Flush(true);

        return NativeLibrary.Load(_extractedDllPath);
    }
}
