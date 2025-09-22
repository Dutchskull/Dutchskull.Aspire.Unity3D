using System.Diagnostics;
using System.Management;

namespace Dutchskull.Aspire.Unity3D.Hosting;

public sealed class UnityProcessManager
{
    public Process? ProcessInstance { get; private set; }

    public Process StartEditor(string editorPath, string projectPath)
    {
        if (!File.Exists(editorPath))
        {
            throw new FileNotFoundException("Unity editor not found", editorPath);
        }

        string args = $"-projectPath \"{Path.GetFullPath(projectPath)}\"";
        ProcessStartInfo startInfo = new()
        {
            FileName = editorPath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(editorPath)
        };
        ProcessInstance = Process.Start(startInfo) ?? throw new InvalidOperationException("Could not start Unity editor");
        ProcessInstance.EnableRaisingEvents = true;
        return ProcessInstance;
    }

    public static Process? FindEditorProcessForProjectAsync(string projectPath)
    {
        projectPath = projectPath.Replace('/', Path.DirectorySeparatorChar).Trim('"');

        if (!OperatingSystem.IsWindows())
        {
            return null;
        }

        static string Escape(string s) => s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("\"", "\\\"");

        string escaped = Escape(projectPath);
        string query = $"SELECT ProcessId, CommandLine, Name, ExecutablePath FROM Win32_Process WHERE CommandLine LIKE \"%{escaped}%\"";
        using ManagementObjectSearcher searcher = new(query);
        foreach (ManagementObject mo in searcher.Get().Cast<ManagementObject>())
        {
            try
            {
                if (mo["ProcessId"] is not uint pid)
                {
                    continue;
                }

                string name = mo["Name"] as string ?? string.Empty;
                string exePath = mo["ExecutablePath"] as string ?? string.Empty;
                if (name.Contains("unity", StringComparison.OrdinalIgnoreCase) &&
                    (name.EndsWith("unity.exe", StringComparison.OrdinalIgnoreCase) ||
                     exePath.EndsWith("Unity.exe", StringComparison.OrdinalIgnoreCase) ||
                     exePath.Contains(Path.Combine("Editor", "Unity.exe"), StringComparison.OrdinalIgnoreCase)))
                {
                    return Process.GetProcessById((int)pid); ;
                }
            }
            catch { }
        }

        return null;
    }
}
