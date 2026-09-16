using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Security.Cryptography;

namespace FPSBooster.App.Services;

/// <summary>Vérifie les mises à jour via un fichier version.txt hébergé (ligne1=version, ligne2=url setup, suite=notes).
/// URL configurable : settings update_url. Vide = vérification désactivée.</summary>
public static class UpdateService
{
    public static string CurrentVersion =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString(3) ?? "1.0.0";

    /// <summary>URL par défaut (releases GitHub). Vide explicite = vérification désactivée.</summary>
    public const string DefaultUpdateUrl =
        "https://github.com/R3333AD/R33333AD-Optimizer/releases/latest/download/version.txt";

    private static readonly HttpClient SharedHttp = new() { Timeout = TimeSpan.FromSeconds(20) };

    public static async Task<(bool Found, string Message)> CheckAsync(bool download = true)
    {
        string url = SettingsService.Get("update_url", DefaultUpdateUrl).Trim();
        if (string.IsNullOrEmpty(url))
            return (false, "Mises à jour : aucune URL configurée (settings → update_url)");

        try
        {
            string text = (await SharedHttp.GetStringAsync(url)).Trim();
            var lines = text.Split('\n').Select(l => l.Trim()).Where(l => l.Length > 0).ToList();
            if (lines.Count == 0) return (false, "Fichier de version vide");
            string remote = lines[0];
            if (!IsNewer(remote, CurrentVersion))
                return (false, $"À jour (v{CurrentVersion})");

            string notes = lines.Count > 2 ? string.Join("\n", lines.Skip(2)) : "Nouvelle version disponible.";
            Logger.Info($"MàJ dispo: {remote} (locale {CurrentVersion})\n{notes}");
            if (download && lines.Count > 1 && lines[1].StartsWith("http"))
            {
                string dest = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "Downloads", $"Setup-R33333ADOptimizer-{remote}.exe");
                Logger.Info($"Téléchargement màj: {lines[1]}");
                byte[] data = await SharedHttp.GetByteArrayAsync(lines[1]);
                // Vérification optionnelle : ligne "sha256:<hex>" après les notes (rétrocompatible si absente)
                string? expected = lines.Skip(3)
                    .FirstOrDefault(l => l.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))?
                    .Substring(7).Trim();
                if (!string.IsNullOrEmpty(expected))
                {
                    string actual = Convert.ToHexString(SHA256.HashData(data));
                    if (!actual.Equals(expected, StringComparison.OrdinalIgnoreCase))
                    {
                        Logger.Warn($"MàJ rejetée : hash SHA256 invalide (attendu {expected}, reçu {actual})");
                        return (true, $"v{remote} disponible (téléchargement rejeté : hash invalide)\n{notes}");
                    }
                }
                await File.WriteAllBytesAsync(dest, data);
                return (true, $"v{remote} téléchargée → {dest}\n{notes}");
            }
            return (true, $"v{remote} disponible !\n{notes}");
        }
        catch (Exception ex)
        {
            Logger.Error("Update check", ex);
            return (false, "Échec vérification : " + ex.Message);
        }
    }

    private static bool IsNewer(string remote, string current)
    {
        try
        {
            return new Version(remote) > new Version(current);
        }
        catch { return false; }
    }
}
