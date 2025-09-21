namespace Dutchskull.Aspire.Unity3D.Hosting;

public static class UnityPathFinder
{
    public static string? GetUnityEditorPathForProject(string projectFolder, string unityVersion, string? customInstallRoot = null)
    {
        if (string.IsNullOrEmpty(projectFolder) || string.IsNullOrEmpty(unityVersion))
        {
            return null;
        }

        string? found = null;

        if (OperatingSystem.IsWindows())
        {
            string programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            string programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);

            if (!string.IsNullOrWhiteSpace(customInstallRoot) && Directory.Exists(customInstallRoot))
            {
                found = FindUnityInRoots([customInstallRoot], unityVersion, WindowsUnityFileNames());
                if (found != null)
                {
                    return found;
                }
            }

            string[] candidates =
            [
                Path.Combine(programFiles, "Unity", "Hub", "Editor", unityVersion, "Editor", "Unity.exe"),
                Path.Combine(programFilesX86, "Unity", "Hub", "Editor", unityVersion, "Editor", "Unity.exe"),
                Path.Combine(programFiles, "Unity", unityVersion, "Editor", "Unity.exe"),
                Path.Combine(programFilesX86, "Unity", unityVersion, "Editor", "Unity.exe"),
                Path.Combine(programFiles, "Unity", "Editor", "Unity.exe")
            ];

            found = candidates.FirstOrDefault(File.Exists);

            if (found == null)
            {
                IEnumerable<string> searchRoots = new[]
                {
                    programFiles,
                    programFilesX86,
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "Local", "Programs")
                }.Where(p => !string.IsNullOrEmpty(p) && Directory.Exists(p)).Distinct();

                found = FindUnityInRoots(searchRoots, unityVersion, WindowsUnityFileNames());
            }
        }
        else if (OperatingSystem.IsMacOS())
        {
            if (!string.IsNullOrWhiteSpace(customInstallRoot) && Directory.Exists(customInstallRoot))
            {
                found = FindUnityInRoots(new[] { customInstallRoot }, unityVersion, MacUnityFileNames());
                if (found != null)
                {
                    return found;
                }
            }

            string[] candidates =
            [
                Path.Combine("/Applications", "Unity", "Hub", "Editor", unityVersion, "Unity.app", "Contents", "MacOS", "Unity"),
                Path.Combine("/Applications", $"Unity-{unityVersion}", "Unity.app", "Contents", "MacOS", "Unity"),
                Path.Combine("/Applications", "Unity.app", "Contents", "MacOS", "Unity")
            ];

            found = candidates.FirstOrDefault(File.Exists);

            if (found == null)
            {
                IEnumerable<string> searchRoots = new[]
                {
                    "/Applications",
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Applications")
                }.Where(Directory.Exists);

                found = FindUnityInRoots(searchRoots, unityVersion, MacUnityFileNames());
            }
        }
        else if (OperatingSystem.IsLinux())
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            if (!string.IsNullOrWhiteSpace(customInstallRoot) && Directory.Exists(customInstallRoot))
            {
                found = FindUnityInRoots(new[] { customInstallRoot }, unityVersion, LinuxUnityFileNames());
                if (found != null)
                {
                    return found;
                }
            }

            string[] candidates =
            [
                Path.Combine(home, ".local", "share", "Unity", "Hub", "Editor", unityVersion, "Editor", "Unity"),
                Path.Combine(home, ".local", "share", "unity3d", "Hub", "Editor", unityVersion, "Editor", "Unity"),
                Path.Combine("/opt", "unity", unityVersion, "Editor", "Unity"),
                Path.Combine("/opt", "Unity", "Editor", "Unity")
            ];

            found = candidates.FirstOrDefault(File.Exists);

            if (found == null)
            {
                IEnumerable<string> searchRoots = new List<string> { home, "/opt", "/usr", "/usr/local" }.Where(Directory.Exists);
                found = FindUnityInRoots(searchRoots, unityVersion, LinuxUnityFileNames());
            }
        }

        if (found == null)
        {
            throw new InvalidOperationException($"Unity editor version '{unityVersion}' not found on this machine.");
        }

        return found;
    }

    public static string? ReadUnityVersionFromProject(string projectFolder)
    {
        string projectVersionPath = Path.Combine(projectFolder, "ProjectSettings", "ProjectVersion.txt");
        if (!File.Exists(projectVersionPath))
        {
            return null;
        }

        string? versionLine;
        try
        {
            versionLine = File.ReadAllLines(projectVersionPath).FirstOrDefault(l => !string.IsNullOrWhiteSpace(l));
        }
        catch
        {
            return null;
        }

        if (string.IsNullOrEmpty(versionLine))
        {
            return null;
        }

        const string prefix = "m_EditorVersion:";
        if (!versionLine.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return versionLine[prefix.Length..].Trim();
    }

    private static string? FindFileFromPatterns(string root, IEnumerable<string> fileNamePatterns)
    {
        foreach (string pattern in fileNamePatterns)
        {
            string candidate = Path.Combine(root, pattern);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? FindInRootByHubFolder(string root, string unityVersion, string fileNamePattern)
    {
        IEnumerable<string> hubMatches = SafeEnumerateDirectories(root, "Hub", SearchOption.AllDirectories);
        foreach (string hub in hubMatches)
        {
            string candidate = Path.Combine(hub, "Editor", unityVersion, "Editor", fileNamePattern);
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? FindInRootByVersion(string root, string unityVersion, IEnumerable<string> fileNamePatterns)
    {
        IEnumerable<string> versionMatches = SafeEnumerateDirectories(root, $"*{unityVersion}*", SearchOption.AllDirectories);
        foreach (string match in versionMatches)
        {
            string? result = FindFileFromPatterns(match, fileNamePatterns);
            if (result != null)
            {
                return result;
            }

            string? deeper = SafeEnumerateFiles(match, fileNamePatterns.First(), SearchOption.AllDirectories).FirstOrDefault();
            if (deeper != null)
            {
                return deeper;
            }
        }

        return null;
    }

    private static string? FindUnityInRoots(IEnumerable<string> roots, string unityVersion, IEnumerable<string> fileNamePatterns)
    {
        foreach (string root in roots)
        {
            try
            {
                string? result = FindInRootByVersion(root, unityVersion, fileNamePatterns);
                if (result != null)
                {
                    return result;
                }

                result = FindInRootByHubFolder(root, unityVersion, fileNamePatterns.First());
                if (result != null)
                {
                    return result;
                }
            }
            catch
            {
            }
        }

        return null;
    }

    private static IEnumerable<string> LinuxUnityFileNames()
    {
        yield return Path.Combine("Editor", "Unity");
    }

    private static IEnumerable<string> MacUnityFileNames()
    {
        yield return Path.Combine("Unity.app", "Contents", "MacOS", "Unity");
    }

    private static IEnumerable<string> SafeEnumerateDirectories(string path, string searchPattern, SearchOption option)
    {
        try
        {
            return Directory.EnumerateDirectories(path, searchPattern, option);
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<string> SafeEnumerateFiles(string path, string searchPattern, SearchOption option)
    {
        try
        {
            return Directory.EnumerateFiles(path, searchPattern, option);
        }
        catch
        {
            return [];
        }
    }

    private static IEnumerable<string> WindowsUnityFileNames()
    {
        yield return Path.Combine("Editor", "Unity.exe");
        yield return Path.Combine("Editor", "Unity", "Unity.exe");
        yield return Path.Combine("Editor", "Data", "Unity.exe");
    }
}
