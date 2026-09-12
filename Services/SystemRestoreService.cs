using System.Threading.Tasks;

namespace DiskOptimizer.Services;

public interface ISystemRestoreService
{
    Task<(bool success, string message)> CreateRestorePointAsync(string description = "Aetherial Suite - Przed optymalizacją");
}

public class SystemRestoreService : ISystemRestoreService
{
    public async Task<(bool success, string message)> CreateRestorePointAsync(string description = "Aetherial Suite - Przed optymalizacją")
    {
        try
        {
            string psCode = $"Checkpoint-Computer -Description '{description.Replace("'", "''")}' -RestorePointType 'MODIFY_SETTINGS' -ErrorAction Stop";
            var result = await DiskHelper.RunPowerShellScriptAsync(psCode);
            if (result.success)
            {
                return (true, "Utworzono punkt przywracania systemu Windows.");
            }
            return (false, "Ochrona systemu Windows jest wyłączona lub limit częstotliwości został osiągnięty.");
        }
        catch (Exception ex)
        {
            return (false, $"Nie można utworzyć punktu przywracania: {ex.Message}");
        }
    }
}
