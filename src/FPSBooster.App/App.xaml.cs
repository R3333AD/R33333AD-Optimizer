using System.Threading;
using System.Windows.Media;
using FPSBooster.App.Models;
using FPSBooster.App.Services;

namespace FPSBooster.App;

public partial class App : System.Windows.Application
{
    private static Mutex? _single;

    protected override void OnStartup(System.Windows.StartupEventArgs e)
    {
        _single = new Mutex(true, "R33333ADOptimizerSingleInstance", out bool first);
        if (!first)
        {
            System.Windows.MessageBox.Show(
                "R33333AD Optimizer est déjà en cours (regarde la zone de notification).",
                "R33333AD Optimizer", System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            Shutdown();
            return;
        }
        if (TryHandleCli(e.Args)) return; // gère Shutdown lui-même
        ApplyTheme();
        base.OnStartup(e);
    }

    /// <summary>CLI : --boost | --clean | --trim | --help. Retourne true si pris en charge.</summary>
    private bool TryHandleCli(string[] args)
    {
        if (args.Length == 0) return false;
        if (args.Any(a => a is "--help" or "-h" or "/?"))
        {
            System.Windows.MessageBox.Show(
                "R33333ADOptimizer.exe [--boost | --clean | --trim | --help]\n\n" +
                "--boost : Boost FPS 1 (avec point de restauration)\n" +
                "--clean : nettoyage fichiers temp\n" +
                "--trim  : Trim RAM (working sets)",
                "R33333AD Optimizer — CLI",
                System.Windows.MessageBoxButton.OK, System.Windows.MessageBoxImage.Information);
            Shutdown();
            return true;
        }
        string? action = args.Select(a => a.ToLowerInvariant())
            .FirstOrDefault(a => a is "--boost" or "--clean" or "--trim") switch
        {
            "--boost" => "boost1",
            "--clean" => "clean_temp",
            "--trim" => "trim_ram",
            _ => null,
        };
        if (action == null) return false;
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
        _ = RunCliAsync(action);
        return true;
    }

    private static async Task RunCliAsync(string actionId)
    {
        try
        {
            if (actionId == "boost1")
                AdminHelper.CreateRestorePoint("FPSBooster - CLI");
            var def = TweakLibrary.ById(actionId);
            if (def == null)
            {
                Logger.Error("CLI action inconnue: " + actionId);
                Current.Shutdown(1);
                return;
            }
            var item = new ToolItem { ActionId = def.ActionId, Title = def.Title };
            var res = await ToolRunner.RunAsync(item);
            Logger.Info($"CLI {actionId} -> {(res.Success ? "OK" : "FAIL")} : {res.Message}");
            Current.Shutdown(res.Success ? 0 : 2);
        }
        catch (Exception ex)
        {
            Logger.Error("CLI", ex);
            Current.Shutdown(3);
        }
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _single?.Dispose();
        _single = null;
        base.OnExit(e);
    }

    public static void ApplyTheme()
    {
        try
        {
            bool dark = SettingsService.GetDarkMode();
            var colors = Current.Resources;
            string C(string name) => dark ? name : "Light_" + name;
            void SetBrush(string brushKey, string colorName, string fallbackHex)
            {
                try
                {
                    Color c;
                    if (colors[C(colorName)] is Color found)
                        c = found;
                    else
                    {
                        Logger.Warn($"ApplyTheme couleur manquante: {C(colorName)}, repli {fallbackHex}");
                        c = (Color)ColorConverter.ConvertFromString(fallbackHex);
                    }
                    // Remplace l'entrée (les brushs XAML sont frozen : on ne peut pas les muter).
                    // Au démarrage ça s'applique partout ; en direct ça s'applique aux DynamicResource.
                    colors[brushKey] = new SolidColorBrush(c);
                }
                catch (Exception ex) { Logger.Warn($"ApplyTheme {brushKey}: {ex.Message}"); }
            }
            void SetColorEntry(string entryKey, string colorName)
            {
                try
                {
                    if (colors[C(colorName)] is Color c)
                        colors[entryKey] = c;
                    else
                        Logger.Warn($"ApplyTheme couleur manquante: {C(colorName)}");
                }
                catch (Exception ex) { Logger.Warn($"ApplyTheme {entryKey}: {ex.Message}"); }
            }
            SetBrush("BgBrush", "BgColor", "#09090B");
            SetBrush("SidebarBrush", "SidebarColor", "#CC101013");
            SetBrush("CardBrush", "CardColor", "#B3141417");
            SetBrush("PanelBrush", "PanelColor", "#B31A1A1F");
            SetBrush("BtnBrush", "BtnColor", "#D81E1E24");
            SetBrush("BorderBrush", "BorderColor", "#3A3A42");
            SetBrush("MutedBrush", "MutedColor", "#8E8E93");
            SetBrush("AccentBrush", "AccentColor", "#C8FF2E");
            // Couleurs utilisées par les animations (résolues par StaticResource) : MAJ pour les fenêtres futures
            SetColorEntry("BorderHoverColor", "BorderHoverColor");
            SetColorEntry("CardHoverColor", "CardHoverColor");
        }
        catch (Exception ex) { Logger.Warn($"ApplyTheme échoue: {ex.Message}"); }
    }
}
