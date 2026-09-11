using System.IO;
using DiskOptimizer.Models;

namespace DiskOptimizer.Services;

public class DiskCleanerService
{
    private readonly CustomTargetService _customService = new();

    public async Task<(long totalFreed, int totalFiles)> CleanItemAsync(
        CleanItem item, 
        Action<string>? logger = null, 
        CancellationToken ct = default)
    {
        if (!item.CanClean)
        {
            logger?.Invoke($"ℹ️ Element '{item.Title}' jest informacyjny. Nie usuwam automatycznie.");
            return (0, 0);
        }

        item.IsCleaning = true;
        item.Status = "Trwa czyszczenie...";
        long freedBytes = 0;
        int deletedFiles = 0;

        try
        {
            logger?.Invoke($"▶ Rozpoczynam czyszczenie: {item.Title}...");

            if (item.ActionType == CleanActionType.RecycleBin)
            {
                long prevSize = item.SizeBytes;
                int prevCount = item.ItemCount;
                bool ok = DiskHelper.EmptyRecycleBin();
                if (ok)
                {
                    freedBytes = prevSize;
                    deletedFiles = prevCount;
                    logger?.Invoke($"  ✓ Kosz systemowy opróżniony ({DriveModel.FormatBytes(freedBytes)}).");
                }
                else
                {
                    logger?.Invoke($"  ⚠️ Wystąpił problem przy opróżnianiu kosza.");
                }
            }
            else if (item.ActionType == CleanActionType.Command && !string.IsNullOrEmpty(item.Command))
            {
                logger?.Invoke($"  Uruchamianie: {item.Command} {item.CommandArgs}...");
                var (ok, _) = await DiskHelper.RunProcessAsync(item.Command, item.CommandArgs ?? "", logger, ct);
                
                if (ok)
                {
                    freedBytes = item.SizeBytes;
                    logger?.Invoke($"  ✓ Zakończono pomyślnie polecenie: {item.Command}.");
                }
                else if (!string.IsNullOrEmpty(item.Path) && Directory.Exists(item.Path))
                {
                    logger?.Invoke($"  Polecenie nie powiodło się, próba bezpośredniego usunięcia plików z: {item.Path}...");
                    var (fBytes, fCount) = await Task.Run(() => DiskHelper.CleanDirectoryContents(item.Path, logger, 0, ct), ct);
                    freedBytes = fBytes;
                    deletedFiles = fCount;
                }
            }
            else if (item.ActionType == CleanActionType.SpecialAction)
            {
                if (item.Id == "nvidia_dxcache")
                {
                    logger?.Invoke("  Czyszczenie pamięci podręcznej shaderów GPU (NVIDIA DXCache, LocalLow, GLCache, D3DSCache, AMD)...");
                    var shaderPaths = DiskScannerService.GetShaderCachePaths();
                    foreach (var sp in shaderPaths)
                    {
                        logger?.Invoke($"  Czyszczenie folderu shaderów: {sp}...");
                        var (fBytes, fCount) = await Task.Run(() => DiskHelper.CleanDirectoryContents(sp, logger, 0, ct), ct);
                        freedBytes += fBytes;
                        deletedFiles += fCount;
                    }
                    logger?.Invoke($"  ✓ Wyczyszczono shadery GPU: zwolniono {DriveModel.FormatBytes(freedBytes)} ({deletedFiles} plików).");
                }
                else if (item.Id == "docker_disk")
                {
                    logger?.Invoke("▶ Rozpoczynam czyszczenie i optymalizację środowiska Docker...");
                    long sizeBefore = File.Exists(item.Path) ? new FileInfo(item.Path).Length : 0;

                    // 1. Jeśli demon dockera jest dostępny, wykonaj prune
                    logger?.Invoke("  Krok 1: Próba usunięcia nieużywanych obrazów i kontenerów (docker system prune)...");
                    try
                    {
                        var (pruneOk, pruneOut) = await DiskHelper.RunProcessAsync("docker", "system prune -a --volumes -f", logger, ct);
                        if (pruneOk) logger?.Invoke("  ✓ Docker system prune wykonany pomyślnie.");
                        else logger?.Invoke("  ℹ️ Demon Docker nie jest aktywny lub brak zbędnych kontenerów.");
                    }
                    catch (Exception pEx)
                    {
                        logger?.Invoke($"  ℹ️ Pomijanie prune: {pEx.Message}");
                    }

                    // 2. Skurcz wirtualny dysk VHDX za pomocą diskpart
                    if (File.Exists(item.Path))
                    {
                        logger?.Invoke($"  Krok 2: Kompaktowanie wirtualnego dysku VHDX ({Path.GetFileName(item.Path)})...");
                        var compactSvc = new CompactOsService();
                        bool shrinkOk = await compactSvc.ShrinkVhdxAsync(item.Path, logger, ct);
                        long sizeAfter = File.Exists(item.Path) ? new FileInfo(item.Path).Length : 0;
                        long vhdxFreed = Math.Max(0, sizeBefore - sizeAfter);
                        if (vhdxFreed > 0)
                        {
                            freedBytes = vhdxFreed;
                            deletedFiles = 1;
                            logger?.Invoke($"  🎉 Sukces! Skurczono dysk Docker VHDX i uwolniono {DriveModel.FormatBytes(freedBytes)}!");
                        }
                        else
                        {
                            freedBytes = 0;
                            deletedFiles = 0;
                            logger?.Invoke("  ✓ Przestrzeń dysku VHDX została zoptymalizowana.");
                        }
                    }
                }
                else if (item.Id == "hiberfil")
                {
                    if (DiskHelper.IsAdministrator())
                    {
                        logger?.Invoke($"  Wyłączanie hibernacji (powercfg /h off)...");
                        var (ok, _) = await DiskHelper.RunProcessAsync("powercfg", "/h off", logger, ct);
                        if (ok)
                        {
                            freedBytes = item.SizeBytes;
                            deletedFiles = 1;
                            logger?.Invoke($"  ✓ Hibernacja wyłączona! Plik hiberfil.sys usunięty.");
                        }
                    }
                    else
                    {
                        logger?.Invoke($"  ⚠️ Wyłączenie hibernacji wymaga uruchomienia programu jako Administrator!");
                    }
                }
            }
            else if (item.ActionType == CleanActionType.DeleteFiles && !string.IsNullOrEmpty(item.Path))
            {
                var (fBytes, fCount) = await Task.Run(() => DiskHelper.CleanDirectoryContents(item.Path, logger, 0, ct), ct);
                freedBytes = fBytes;
                deletedFiles = fCount;
                logger?.Invoke($"  ✓ Usunięto {deletedFiles} plików ({DriveModel.FormatBytes(freedBytes)}).");
            }

            // Odśwież status elementu
            item.SizeBytes = Math.Max(0, item.SizeBytes - freedBytes);
            item.Status = freedBytes > 0 
                ? $"Wyczyszczono: {DriveModel.FormatBytes(freedBytes)}"
                : (item.SizeBytes == 0 ? "Czysto (0 B)" : "Pliki w użyciu przez aktywny proces");
        }
        catch (Exception ex)
        {
            item.Status = $"Błąd: {ex.Message}";
            logger?.Invoke($"  ❌ Błąd czyszczenia {item.Title}: {ex.Message}");
        }
        finally
        {
            item.IsCleaning = false;
        }

        return (freedBytes, deletedFiles);
    }

    public async Task<(long freedBytes, int deletedFiles)> CleanCustomTargetAsync(
        CustomTarget target,
        Action<string>? logger = null,
        CancellationToken ct = default)
    {
        return await _customService.CleanTargetAsync(target, logger, ct);
    }

    public async Task<bool> RunDockerPruneAsync(Action<string>? logger = null, CancellationToken ct = default)
    {
        logger?.Invoke("▶ Uruchamianie czyszczenia Dockera: docker system prune -f --volumes...");
        var (ok, output) = await DiskHelper.RunProcessAsync("docker", "system prune -f --volumes", logger, ct);
        if (ok)
        {
            logger?.Invoke("✓ Docker system prune zakończony pomyślnie!");
        }
        else
        {
            logger?.Invoke($"⚠️ Błąd Dockera: {output}");
        }
        return ok;
    }

    public async Task<bool> SetHibernationModeAsync(string mode, Action<string>? logger = null, CancellationToken ct = default)
    {
        if (!DiskHelper.IsAdministrator())
        {
            logger?.Invoke("⚠️ Do zmiany stanu hibernacji wymagane są uprawnienia Administratora!");
            return false;
        }

        string args = mode.ToLowerInvariant() switch
        {
            "off" => "/h off",
            "reduced" => "/h /type reduced",
            "full" => "/h /type full",
            "on" => "/h on",
            _ => "/h on"
        };

        logger?.Invoke($"▶ Wykonywanie: powercfg {args}...");
        var (ok, output) = await DiskHelper.RunProcessAsync("powercfg", args, logger, ct);
        if (ok)
        {
            logger?.Invoke($"✓ Stan hibernacji zmieniony na '{mode}'.");
        }
        else
        {
            logger?.Invoke($"❌ Błąd powercfg: {output}");
        }
        return ok;
    }
}
