using System.Diagnostics;
using System.Management;

namespace Dutchskull.Aspire.Unity3D.Hosting;

public sealed class UnityProcessManager {
    public Process? ProcessInstance { get; private set; }

    public static Process? FindEditorProcessForProjectAsync(string projectPath) {
        projectPath = projectPath.Replace('/', Path.DirectorySeparatorChar).Trim('"');

        if (OperatingSystem.IsWindows()) {
            return FindEditorProcessOnWindows(projectPath);
        }

        if (OperatingSystem.IsLinux() || OperatingSystem.IsMacOS()) {
            return FindEditorProcessOnUnix(projectPath);
        }

        return null;
    }

    public Process StartEditor(string editorPath, string projectPath) {
        if (!File.Exists(editorPath)) {
            throw new FileNotFoundException("Unity editor not found", editorPath);
        }

        string args = $"-projectPath \"{Path.GetFullPath(projectPath)}\"";
        ProcessStartInfo startInfo = new() {
            FileName = editorPath,
            Arguments = args,
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = Path.GetDirectoryName(editorPath),
        };
        ProcessInstance = Process.Start(startInfo) ??
                          throw new InvalidOperationException("Could not start Unity editor");
        ProcessInstance.EnableRaisingEvents = true;
        return ProcessInstance;
    }

    private static Process? FindEditorProcessOnUnix(string projectPath) {
        Process[] processes = Process.GetProcesses();

        foreach (Process process in processes) {
            try {
                if (process.HasExited || !IsUnityProcessName(process.ProcessName)) {
                    continue;
                }

                if (ProcessContainsProjectPath(process, projectPath)) {
                    return process;
                }
            }
            catch {
            }
        }

        return null;
    }

    private static Process? FindEditorProcessOnWindows(string projectPath) {
        if (!OperatingSystem.IsWindows()) {
            return null;
        }

        static string Escape(string s) => s.Replace("\\", "\\\\").Replace("%", "\\%").Replace("\"", "\\\"");

        string escaped = Escape(projectPath);
        string query =
            $"SELECT ProcessId, CommandLine, Name, ExecutablePath FROM Win32_Process WHERE CommandLine LIKE \"%{escaped}%\"";
        using ManagementObjectSearcher searcher = new(query);
        foreach (ManagementObject mo in searcher.Get().Cast<ManagementObject>()) {
            try {
                if (mo["ProcessId"] is not uint pid) {
                    continue;
                }

                string name = mo["Name"] as string ?? string.Empty;
                string exePath = mo["ExecutablePath"] as string ?? string.Empty;
                if (name.Contains("unity", StringComparison.OrdinalIgnoreCase) &&
                    (name.EndsWith("unity.exe", StringComparison.OrdinalIgnoreCase) ||
                     exePath.EndsWith("Unity.exe", StringComparison.OrdinalIgnoreCase) ||
                     exePath.Contains(Path.Combine("Editor", "Unity.exe"), StringComparison.OrdinalIgnoreCase))) {
                    return Process.GetProcessById((int)pid);
                    ;
                }
            }
            catch {
            }
        }

        return null;
    }

    private static bool IsUnityProcessName(string processName) =>
        processName.Contains("unity", StringComparison.OrdinalIgnoreCase) ||
        processName.Contains("Unity", StringComparison.Ordinal);

    private static bool ProcessContainsProjectPath(Process process, string projectPath) {
        if (OperatingSystem.IsLinux()) {
            string cmdlineFile = $"/proc/{process.Id}/cmdline";
            if (!File.Exists(cmdlineFile)) {
                return false;
            }

            string cmdlineContent = File.ReadAllText(cmdlineFile);
            string[] args = cmdlineContent.Split('\0', StringSplitOptions.RemoveEmptyEntries);

            for (int i = 0; i < args.Length; i++) {
                if (!args[i].Equals("-projectpath", StringComparison.OrdinalIgnoreCase) || i + 1 >= args.Length) {
                    continue;
                }

                string argProjectPath = args[i + 1].Trim('"');
                return string.Equals(
                    Path.GetFullPath(argProjectPath),
                    Path.GetFullPath(projectPath));
            }
        }

        string arguments = process.StartInfo.Arguments;
        return !string.IsNullOrEmpty(arguments) && arguments.Contains(projectPath, StringComparison.OrdinalIgnoreCase);
    }
}