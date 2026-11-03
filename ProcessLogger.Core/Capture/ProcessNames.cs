namespace ProcessLogger.Core.Capture;

/// <summary>
/// One spelling for a process's image name across sources. WMI reports "cmd.exe"; the ETW kernel trace
/// (via TraceEvent) reports "cmd". Without normalizing, a row's Name depended on which source arrived first.
/// Rows are stored with the ".exe" form; filters accept either form so rows written before this still match.
/// </summary>
public static class ProcessNames
{
    // Kernel pseudo-processes that have no image file, so no ".exe" to add.
    private static readonly HashSet<string> Pseudo = new(StringComparer.OrdinalIgnoreCase)
    {
        "Idle", "System", "Registry", "Memory Compression", "Secure System", "vmmem", "MemCompression",
    };

    /// <summary>The stored form: the name with ".exe" when it's an image without an extension yet.</summary>
    public static string Normalize(string? name, string? path = null)
    {
        if (string.IsNullOrWhiteSpace(name)) return name ?? "";
        name = name.Trim();
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || Pseudo.Contains(name)) return name;
        // The image path, when known, gives the real file name (e.g. a ".com" or ".scr" image).
        var file = System.IO.Path.GetFileName(path);
        if (!string.IsNullOrEmpty(file)
            && string.Equals(System.IO.Path.GetFileNameWithoutExtension(file), name, StringComparison.OrdinalIgnoreCase))
            return file;
        return name + ".exe";
    }

    /// <summary>The other spelling of <paramref name="name"/> ("cmd" for "cmd.exe" and back), or null when it has
    /// none; used so filters match rows stored before names were normalized.</summary>
    public static string? AlternateSpelling(string? name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return name[..^4];
        return Pseudo.Contains(name) ? null : name + ".exe";
    }

    /// <summary>True when both name the same image, whichever spelling each uses.</summary>
    public static bool SameImage(string? a, string? b) =>
        string.Equals(Normalize(a), Normalize(b), StringComparison.OrdinalIgnoreCase);
}
