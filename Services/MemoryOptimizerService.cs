using System.Diagnostics;
using System.Runtime.InteropServices;
using DiskOptimizer.Models;

namespace DiskOptimizer.Services;

public class MemoryOptimizerService
{
    [DllImport("psapi.dll")]
    private static extern int EmptyWorkingSet(IntPtr hwProc);

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public class MEMORYSTATUSEX
    {
        public uint dwLength;
        public uint dwMemoryLoad;
        public ulong ullTotalPhys;
        public ulong ullAvailPhys;
        public ulong ullTotalPageFile;
        public ulong ullAvailPageFile;
        public ulong ullTotalVirtual;
        public ulong ullAvailVirtual;
        public ulong ullAvailExtendedVirtual;
        public MEMORYSTATUSEX() { dwLength = (uint)Marshal.SizeOf(typeof(MEMORYSTATUSEX)); }
    }

    [return: MarshalAs(UnmanagedType.Bool)]
    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    public static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX lpBuffer);

    public static (ulong totalBytes, ulong availBytes, uint loadPercent) GetMemoryMetrics()
    {
        try
        {
            var mem = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(mem))
            {
                return (mem.ullTotalPhys, mem.ullAvailPhys, mem.dwMemoryLoad);
            }
        }
        catch { }
        return (0, 0, 0);
    }

    public async Task<(long freedBytes, int processedProcesses)> OptimizeRamAsync(Action<string>? logger = null, CancellationToken ct = default)
    {
        var before = GetMemoryMetrics();
        logger?.Invoke($"Rozpoczynam optymalizację pamięci RAM (Aktualne obciążenie: {before.loadPercent}%)...");

        int processed = 0;
        await Task.Run(() =>
        {
            var processes = Process.GetProcesses();
            foreach (var proc in processes)
            {
                if (ct.IsCancellationRequested) break;
                try
                {
                    // Pomiń procesy krytyczne systemu
                    if (proc.Id <= 4 || proc.ProcessName.Equals("System", StringComparison.OrdinalIgnoreCase))
                        continue;

                    EmptyWorkingSet(proc.Handle);
                    processed++;
                }
                catch
                {
                    // Odmowa dostępu do procesów chronionych
                }
                finally
                {
                    proc.Dispose();
                }
            }
        }, ct);

        // Odczekaj chwilę na aktualizację metryk Windows
        await Task.Delay(300, ct);
        var after = GetMemoryMetrics();

        long freed = (long)(after.availBytes - before.availBytes);
        if (freed < 0) freed = 0;

        logger?.Invoke($"✓ Zoptymalizowano pamięć RAM dla {processed} procesów.");
        logger?.Invoke($"✓ Nowe obciążenie pamięci: {after.loadPercent}% (Udostępniono szacunkowo: {DriveModel.FormatBytes(freed)}).");

        return (freed, processed);
    }
}
