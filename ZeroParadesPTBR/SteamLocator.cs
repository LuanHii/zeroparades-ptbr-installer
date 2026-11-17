using System.Runtime.Versioning;
using System.Text.RegularExpressions;
using Microsoft.Win32;

namespace ZeroParadesPTBR;

public static partial class SteamLocator
{
    private const string PreferredPrefix = "g5ibkj7vdwf2g67g_assets_all_";
    private const string BundlePattern = "*_assets_all_*.bundle";

    private static readonly string[] GameFolderNames =
    [
        "Zero Parades",
        "ZeroParades",
        "Zero Parades For Dead Spies"
    ];

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"")]
    private static partial Regex LibraryPathRegex();

    public static string? FindBundle()
    {
        foreach (var library in GetSteamLibraries())
        {
            var common = Path.Combine(library, "steamapps", "common");
            foreach (var gameDir in GameFolderNames)
            {
                var bundleDir = Path.Combine(common, gameDir, "ZeroParades_Data", "StreamingAssets", "aa", "StandaloneWindows64");
                var found = FindBundleInDirectory(bundleDir);
                if (found is not null) return found;
            }

            var expectedGame = Path.Combine(common, "Zero Parades");
            if (Directory.Exists(expectedGame))
            {
                var any = Directory.EnumerateFiles(expectedGame, BundlePattern, SearchOption.AllDirectories)
                    .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (any is not null) return any;
            }
        }

        return null;
    }

    public static string? ResolveBundleFromPath(string path)
    {
        if (File.Exists(path))
        {
            var name = Path.GetFileName(path);
            if (name.Contains("_assets_all_", StringComparison.OrdinalIgnoreCase) &&
                name.EndsWith(".bundle", StringComparison.OrdinalIgnoreCase))
            {
                return path;
            }
            return null;
        }

        if (!Directory.Exists(path)) return null;

        var direct = FindBundleInDirectory(path);
        if (direct is not null) return direct;

        var sub = Path.Combine(path, "ZeroParades_Data", "StreamingAssets", "aa", "StandaloneWindows64");
        direct = FindBundleInDirectory(sub);
        if (direct is not null) return direct;

        return Directory.EnumerateFiles(path, BundlePattern, SearchOption.AllDirectories)
            .OrderByDescending(p => Path.GetFileName(p).StartsWith(PreferredPrefix, StringComparison.OrdinalIgnoreCase))
            .ThenBy(p => p, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
    }

    private static string? FindBundleInDirectory(string dir)
    {
        if (!Directory.Exists(dir)) return null;
        var files = Directory.GetFiles(dir, BundlePattern, SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (files.Length == 0) return null;

        return files.FirstOrDefault(f =>
            Path.GetFileName(f).StartsWith(PreferredPrefix, StringComparison.OrdinalIgnoreCase))
            ?? files[0];
    }

    private static IEnumerable<string> GetSteamLibraries()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var libraries = new List<string>();

        foreach (var root in GetSteamRoots())
        {
            if (Directory.Exists(Path.Combine(root, "steamapps")) && seen.Add(root))
            {
                libraries.Add(root);
            }

            var vdf = Path.Combine(root, "steamapps", "libraryfolders.vdf");
            if (!File.Exists(vdf)) continue;

            var content = File.ReadAllText(vdf);
            foreach (Match match in LibraryPathRegex().Matches(content))
            {
                var libPath = match.Groups[1].Value.Replace("\\\\", "\\");
                if (Directory.Exists(libPath) && seen.Add(libPath))
                {
                    libraries.Add(libPath);
                }
            }
        }

        return libraries;
    }

    private static IEnumerable<string> GetSteamRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (OperatingSystem.IsWindows())
        {
            AddWindowsSteamRoots(roots);
        }
        else
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            foreach (var candidate in new[]
            {
                Path.Combine(home, ".local", "share", "Steam"),
                Path.Combine(home, ".steam", "steam"),
                Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"),
                "/run/media/mmcblk0p1/steamapps"
            })
            {
                if (Directory.Exists(candidate)) roots.Add(candidate);
            }
        }

        return roots;
    }

    [SupportedOSPlatform("windows")]
    private static void AddWindowsSteamRoots(HashSet<string> roots)
    {
        foreach (var (hive, subKey) in new[]
        {
            (Registry.CurrentUser, @"Software\Valve\Steam"),
            (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Valve\Steam"),
            (Registry.LocalMachine, @"SOFTWARE\Valve\Steam")
        })
        {
            try
            {
                using var key = hive.OpenSubKey(subKey);
                if (key is null) continue;
                foreach (var valName in new[] { "SteamPath", "InstallPath" })
                {
                    if (key.GetValue(valName) is string p && Directory.Exists(p))
                    {
                        roots.Add(p);
                    }
                }
            }
            catch
            {
                // Ignore inaccessible registry keys
            }
        }

        foreach (var drive in DriveInfo.GetDrives())
        {
            if (!drive.IsReady) continue;
            foreach (var rel in new[] { "SteamLibrary", "Steam", @"Program Files (x86)\Steam", @"Program Files\Steam" })
            {
                var candidate = Path.Combine(drive.RootDirectory.FullName, rel);
                if (Directory.Exists(candidate)) roots.Add(candidate);
            }
        }
    }
}
