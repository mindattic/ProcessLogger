namespace ProcessLogger.Core.Data;

/// <summary>
/// One window taking OS foreground focus. Cross-referencing this against <see cref="ProcessEvent"/>
/// rows is what actually answers "what stole focus from my game," not the process log alone.
/// </summary>
public sealed class FocusEvent
{
    public long Id { get; set; }

    public int ProcessId { get; set; }
    public string? ProcessName { get; set; }
    public string? WindowTitle { get; set; }

    public DateTime FocusedAtUtc { get; set; }

    /// <summary>Set when the next foreground change fires; null while this window still has focus.</summary>
    public DateTime? FocusLostAtUtc { get; set; }
}
