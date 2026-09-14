namespace DiskOptimizer.Models;
public sealed record CleanupReport(long FreedBytes, int DeletedFiles, int Errors, bool Cancelled, string Summary);
