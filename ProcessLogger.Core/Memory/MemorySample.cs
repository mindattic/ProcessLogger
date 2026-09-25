namespace ProcessLogger.Core.Memory;

/// <summary>Result of sampling a process's memory over its lifetime. All fields null/0 when the
/// process exited before a single sample could be taken.</summary>
public readonly record struct MemorySample(
    long? PeakWorkingSetBytes,
    long? PeakPrivateBytes,
    long? FinalWorkingSetBytes,
    int SampleCount);
