namespace DiskOptimizer.Services;
public class SystemRestoreService : ISystemRestoreService
{
    public async Task<(bool success, string message)> CreateRestorePointAsync(string description = "Aetherial - Przed czyszczeniem", CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        if (!DiskHelper.IsAdministrator()) return (false, "Utworzenie punktu przywracania wymaga uprawnień administratora.");
        try
        {
            string escaped = description.Replace("'", "''");
            // Checkpoint-Computer may only warn and exit 0 when throttled. Verify a new sequence number.
            string script = $"$before = @(Get-ComputerRestorePoint -ErrorAction Stop | Select-Object -ExpandProperty SequenceNumber); Checkpoint-Computer -Description '{escaped}' -RestorePointType MODIFY_SETTINGS -ErrorAction Stop -WarningAction Stop; $new = @(Get-ComputerRestorePoint -ErrorAction Stop | Where-Object {{ $_.SequenceNumber -notin $before -and $_.Description -eq '{escaped}' }}); if ($new.Count -eq 0) {{ throw 'Nie potwierdzono nowego punktu przywracania.' }}; Write-Output 'AETHERIAL_RESTORE_CONFIRMED'";
            var result = await DiskHelper.RunPowerShellScriptAsync(script, ct: ct).ConfigureAwait(false);
            ct.ThrowIfCancellationRequested();
            return result.success && result.output.Contains("AETHERIAL_RESTORE_CONFIRMED", StringComparison.Ordinal)
                ? (true, "Potwierdzono nowy punkt przywracania Windows. Nie obejmuje kopii usuwanych plików.")
                : (false, $"Nie utworzono punktu przywracania (kod {result.exitCode}): {result.output}");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex) { return (false, $"Nie można utworzyć punktu przywracania: {ex.Message}"); }
    }
}
