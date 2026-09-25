namespace ProcessLogger.Core.Capture;

/// <summary>
/// The classic NT Kernel Logger's Process ETW events carry only the short image name, not a full
/// path — the full path (when present) shows up as the leading token of the command line instead.
/// This is a best-effort heuristic, not a guarantee: some processes report a relative or bare
/// command name with no resolvable path at all.
/// </summary>
internal static class ProcessPathHeuristics
{
    public static string? TryExtractPathFromCommandLine(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return null;
        }

        var trimmed = commandLine.TrimStart();
        string candidate;
        if (trimmed.StartsWith('"'))
        {
            var closingQuote = trimmed.IndexOf('"', 1);
            if (closingQuote < 0)
            {
                return null;
            }
            candidate = trimmed[1..closingQuote];
        }
        else
        {
            var spaceIndex = trimmed.IndexOf(' ');
            candidate = spaceIndex < 0 ? trimmed : trimmed[..spaceIndex];
        }

        return Path.IsPathRooted(candidate) ? candidate : null;
    }
}
