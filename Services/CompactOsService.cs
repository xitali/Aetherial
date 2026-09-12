using System.IO;

namespace DiskOptimizer.Services;

public class CompactOsService
{
    public async Task<bool> CompressFolderAsync(string folderPath, Action<string>? logger = null, CancellationToken ct = default)
    {
        if (!Directory.Exists(folderPath))
        {
            logger?.Invoke($"Katalog nie istnieje: {folderPath}");
            return false;
        }

        logger?.Invoke($"Rozpoczynam bezstratną kompresję NTFS (XPRESS8K) dla: {folderPath}...");
        logger?.Invoke("Pliki pozostaną w pełni dostępne do odczytu bez rozpakowywania.");

        var (ok, output) = await DiskHelper.RunProcessAsync(
            "compact.exe", 
            $"/c /s:\"{folderPath}\" /a /i /exe:xpress8k", 
            logger, 
            ct);

        if (ok)
        {
            logger?.Invoke("✓ Zakończono kompresję folderu algorytmem CompactOS!");
        }
        else
        {
            logger?.Invoke($"Błąd compact.exe: {output}");
        }

        return ok;
    }

    public async Task<bool> ShrinkVhdxAsync(string vhdxPath, Action<string>? logger = null, CancellationToken ct = default)
    {
        if (!File.Exists(vhdxPath))
        {
            logger?.Invoke($"Plik dysku VHDX nie istnieje: {vhdxPath}");
            return false;
        }

        vhdxPath = Path.GetFullPath(vhdxPath);
        if (!vhdxPath.EndsWith(".vhdx", StringComparison.OrdinalIgnoreCase) || vhdxPath.IndexOfAny(new[] { '"', '\r', '\n' }) >= 0 || !DiskHelper.IsAdministrator())
        {
            logger?.Invoke("Kompaktowanie wymaga pliku VHDX oraz uprawnień Administratora.");
            return false;
        }
        logger?.Invoke($"=== Rozpoczynam kompaktowanie dysku VHDX ({Path.GetFileName(vhdxPath)}) ===");
        logger?.Invoke("1. Zatrzymywanie instancji WSL (wsl --shutdown)...");
        await DiskHelper.RunProcessAsync("wsl", "--shutdown", logger, ct);

        // Przygotuj skrypt diskpart
        string scriptPath = Path.Combine(Path.GetTempPath(), $"compact_vhdx_{Guid.NewGuid():N}.txt");
        try
        {
            string scriptContent = $"select vdisk file=\"{vhdxPath}\"\r\nattach vdisk readonly\r\ncompact vdisk\r\ndetach vdisk\r\n";
            await File.WriteAllTextAsync(scriptPath, scriptContent, System.Text.Encoding.Unicode, ct);

            logger?.Invoke("2. Uruchamianie procedury diskpart compact vdisk...");
            var (ok, _) = await DiskHelper.RunProcessAsync("diskpart", $"/s \"{scriptPath}\"", logger, ct);

            if (ok)
            {
                logger?.Invoke("🎉 Zakończono kompaktowanie dysku VHDX! Pusta przestrzeń została uwolniona.");
                return true;
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            logger?.Invoke($"❌ Błąd diskpart: {ex.Message}");
        }
        finally
        {
            if (File.Exists(scriptPath)) File.Delete(scriptPath);
        }

        return false;
    }
}
