using Microsoft.EntityFrameworkCore.Design;
using ProcessLogger.Core.Options;

namespace ProcessLogger.Core.Data;

/// <summary>Design-time factory so <c>dotnet ef migrations add</c> can construct the context
/// without needing a full DI host — it never runs at app runtime.</summary>
public sealed class ProcessLoggerDbContextFactory : IDesignTimeDbContextFactory<ProcessLoggerDbContext>
{
    public ProcessLoggerDbContext CreateDbContext(string[] args)
    {
        var options = ProcessLoggerDbContext.BuildOptions(ProcessLoggerOptions.DefaultDatabasePath);
        return new ProcessLoggerDbContext(options);
    }
}
