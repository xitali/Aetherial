using System;

namespace DiskOptimizer.Models;

public class HistoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime Timestamp { get; set; } = DateTime.Now;
    public string OperationType { get; set; } = "Optymalizacja";
    public int ItemsFixed { get; set; }
    public long BytesSaved { get; set; }
    public bool Success { get; set; } = true;
    public string Summary { get; set; } = string.Empty;

    public string FormattedDate => Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
    public string FormattedBytes => BytesSaved > 0 ? DriveModel.FormatBytes(BytesSaved) : "0 B";
    public string StatusBadge => Success ? "✓ Sukces" : (ItemsFixed > 0 ? "⚠ Częściowo" : "⚠ Nieukończona");
    public string StatusColor => Success ? "#10B981" : "#F59E0B";
}
