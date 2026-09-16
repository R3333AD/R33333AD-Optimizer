using System.IO;
using System.IO.Compression;
using System.Text.Json;
using FPSBooster.App.Models;

namespace FPSBooster.App.Services;

/// <summary>Export/import de la config (settings, gains, benchmarks) en .zip.
/// L'import exige un redémarrage (cache SettingsService chargé une fois).</summary>
public static class ConfigBackupService
{
    private static readonly string AppDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FPSBooster");

    private static readonly string[] Files = { "settings.txt", "fpsgains.json", "bench.json", "bench-history.json" };

    public static TweakResult Export(string? destDir = null)
    {
        string tmp = Path.Combine(Path.GetTempPath(), "fpsbooster-config-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(tmp);
            int n = 0;
            foreach (var f in Files)
            {
                string src = Path.Combine(AppDir, f);
                if (!File.Exists(src)) continue;
                File.Copy(src, Path.Combine(tmp, f), overwrite: true);
                n++;
            }
            if (n == 0) return new(false, "Rien à sauvegarder");
            string dir = destDir ?? Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
            Directory.CreateDirectory(dir);
            string dest = Path.Combine(dir, $"R33333AD-config-{DateTime.Now:yyyyMMdd-HHmm}.zip");
            if (File.Exists(dest)) File.Delete(dest);
            ZipFile.CreateFromDirectory(tmp, dest);
            Logger.Info($"Config exportée: {dest} ({n} fichiers)");
            return new(true, dest);
        }
        catch (Exception ex)
        {
            Logger.Error("Config export", ex);
            return new(false, "Export échoué : " + ex.Message);
        }
        finally
        {
            try { if (Directory.Exists(tmp)) Directory.Delete(tmp, recursive: true); }
            catch (Exception ex) { Logger.Warn($"Config export nettoyage tmp: {ex.Message}"); }
        }
    }

    public static TweakResult ImportFile(string zipPath)
    {
        string tmp = Path.Combine(Path.GetTempPath(), "fpsbooster-config-" + Guid.NewGuid().ToString("N"));
        try
        {
            if (!File.Exists(zipPath)) return new(false, "Archive introuvable");
            using (var zip = ZipFile.OpenRead(zipPath))
            {
                var names = zip.Entries.Select(e => e.Name).ToList();
                if (!names.Any(n => Files.Contains(n, StringComparer.OrdinalIgnoreCase)))
                    return new(false, "Archive invalide (aucun fichier connu)");
            }
            Directory.CreateDirectory(tmp);
            ZipFile.ExtractToDirectory(zipPath, tmp, overwriteFiles: true);
            Directory.CreateDirectory(AppDir);
            int n = 0;
            foreach (var f in Files)
            {
                string src = Path.Combine(tmp, f);
                if (!File.Exists(src)) continue;
                if (f.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    try { JsonDocument.Parse(File.ReadAllText(src)); }
                    catch
                    {
                        Logger.Warn($"Config import ignore {f} (JSON invalide)");
                        continue;
                    }
                }
                File.Copy(src, Path.Combine(AppDir, f), overwrite: true);
                n++;
            }
            Logger.Info($"Config importée depuis {zipPath}: {n} fichiers (redémarrage requis)");
            return n > 0
                ? new(true, $"{n} fichiers restaurés — redémarre l'app")
                : new(false, "Rien à restaurer");
        }
        catch (Exception ex)
        {
            Logger.Error("Config import", ex);
            return new(false, "Import échoué : " + ex.Message);
        }
        finally
        {
            try { if (Directory.Exists(tmp)) Directory.Delete(tmp, recursive: true); }
            catch (Exception ex) { Logger.Warn($"Config import nettoyage tmp: {ex.Message}"); }
        }
    }

    public static TweakResult ImportWithDialog()
    {
        try
        {
            var dlg = new Microsoft.Win32.OpenFileDialog
            {
                Filter = "R33333AD config (*.zip)|*.zip",
                Title = "Importer une config R33333AD"
            };
            if (dlg.ShowDialog() != true) return new(false, "Import annulé");
            return ImportFile(dlg.FileName);
        }
        catch (Exception ex)
        {
            Logger.Error("Config import dialogue", ex);
            return new(false, "Import échoué : " + ex.Message);
        }
    }
}
