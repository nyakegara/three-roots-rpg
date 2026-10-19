using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.Win32.SafeHandles;

// Resolve the whole I/O plan before opening images or creating any output directory.
static class PathSafety
{
    public static void Validate(Dictionary<string, string> options)
    {
        string Required(string key) => options.TryGetValue(key, out var value) ? Path.GetFullPath(value) : throw new ArgumentException($"Missing {key}");
        var source = Required("--source"); var output = Required("--output");
        var inputs = new List<Entry> { new("primary source", source) };
        if (options.TryGetValue("--row-map", out var map))
        {
            map = Path.GetFullPath(map); inputs.Add(new Entry("row map", map));
            using var json = JsonDocument.Parse(File.ReadAllText(map));
            foreach (var row in json.RootElement.EnumerateArray())
                inputs.Add(new Entry("row-map source", Path.GetFullPath(row.GetProperty("source").GetString()!, Path.GetDirectoryName(map)!)));
        }
        var writes = new List<Entry> { new("output PNG", output), new("metadata JSON", Path.ChangeExtension(output, ".json")) };
        if (options.TryGetValue("--source-copy", out var copy))
        {
            copy = Path.GetFullPath(copy);
            // Only the exact primary pathname is the documented no-op. Other aliases
            // (including hard links) must reject rather than copy over an input.
            if (!copy.Equals(source, StringComparison.OrdinalIgnoreCase)) writes.Add(new Entry("source copy", copy));
        }
        if (options.TryGetValue("--contact-strip", out var strip)) writes.Add(new Entry("contact strip", Path.GetFullPath(strip)));
        for (var index = 0; index < writes.Count; index++)
        {
            foreach (var input in inputs)
                if (SameFile(writes[index], input)) Fail(writes[index], input);
            for (var previous = 0; previous < index; previous++)
                if (SameFile(writes[index], writes[previous])) Fail(writes[index], writes[previous]);
        }
    }

    static void Fail(Entry first, Entry second) => throw new ArgumentException($"Path collision: {first.Role} '{first.Path}' aliases {second.Role} '{second.Path}'. No files were written.");
    static bool SameFile(Entry a, Entry b) => a.Canonical.Equals(b.Canonical, StringComparison.OrdinalIgnoreCase)
        || a.Identity is not null && a.Identity == b.Identity;

    sealed class Entry(string role, string path)
    {
        public string Role { get; } = role;
        public string Path { get; } = path;
        public string Canonical { get; } = CanonicalPath(path);
        public string? Identity { get; } = FileIdentity(path);
    }

    static string CanonicalPath(string path)
    {
        var full = Path.GetFullPath(path); var root = Path.GetPathRoot(full)!; var current = root;
        foreach (var segment in full[root.Length..].Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, segment.TrimEnd(' ', '.'));
            FileSystemInfo? existing = Directory.Exists(current) ? new DirectoryInfo(current) : File.Exists(current) ? new FileInfo(current) : null;
            if (existing?.LinkTarget is not null) current = existing.ResolveLinkTarget(true)!.FullName;
        }
        return Path.GetFullPath(current);
    }

    static string? FileIdentity(string path)
    {
        if (!File.Exists(path)) return null;
        using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return GetFileInformationByHandle(handle, out var info)
            ? $"{info.VolumeSerialNumber:x8}:{info.FileIndexHigh:x8}:{info.FileIndexLow:x8}"
            : throw new IOException($"Cannot verify file identity: {path}", Marshal.GetExceptionForHR(Marshal.GetHRForLastWin32Error()));
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    static extern bool GetFileInformationByHandle(SafeFileHandle file, out ByHandleFileInformation information);
    [StructLayout(LayoutKind.Sequential)]
    struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public System.Runtime.InteropServices.ComTypes.FILETIME CreationTime, LastAccessTime, LastWriteTime;
        public uint VolumeSerialNumber, FileSizeHigh, FileSizeLow, NumberOfLinks, FileIndexHigh, FileIndexLow;
    }
}
