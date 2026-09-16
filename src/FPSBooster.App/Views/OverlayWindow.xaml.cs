using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Threading;
using FPSBooster.App.Services;

namespace FPSBooster.App.Views;

public partial class OverlayWindow : Window
{
    private readonly MonitoringService _monitor = new();
    private readonly DispatcherTimer _timer = new() { Interval = TimeSpan.FromSeconds(1) };
    private int _tick;
    private string _ping = "—";

    public OverlayWindow()
    {
        InitializeComponent();
        // Position restaurée si sauvegardée, sinon haut-droite de l'écran principal
        try
        {
            if (double.TryParse(SettingsService.Get("overlay_x", ""),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double x)
                && double.TryParse(SettingsService.Get("overlay_y", ""),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out double y))
            {
                Left = x;
                Top = y;
            }
            else
            {
                Left = SystemParameters.PrimaryScreenWidth - Width - 20;
                Top = 20;
            }
        }
        catch { }
        if (SettingsService.Get("overlay_clickthrough", "0") == "1")
            ClickThroughBox.IsChecked = true;
        Loaded += (_, _) =>
        {
            if (ClickThroughBox.IsChecked == true)
                ClickThrough_Changed(ClickThroughBox, new RoutedEventArgs());
        };
        _timer.Tick += async (_, _) => await RefreshAsync();
        _timer.Start();
        _ = RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        try
        {
            _tick++;
            double cpu = Math.Round(_monitor.GetCpuPercent(), 0);
            double ram = Math.Round(_monitor.GetRamPercent(), 0);
            CpuText.Text = $"{cpu:0}%";
            RamText.Text = $"{ram:0}%";
            GpuText.Text = _monitor.GetGpuText();
            if (_tick % 5 == 0)
            {
                _ping = await QuickPingAsync();
                FpsText.Text = FpsGainService.LastFpsText();
                FpsText.ToolTip = FpsGainService.LastFpsTip();
            }
            PingText.Text = _ping;
            if (_tick % 3 == 0)
            {
                var games = ProcessService.DetectRunningGames();
                GameText.Text = games.Length > 0 ? "🎮 " + string.Join(", ", games) : "";
            }
        }
        catch { }
    }

    private static async Task<string> QuickPingAsync()
    {
        try
        {
            var (code, stdout, _) = await CmdHelper.RunAsync("ping.exe", "-n 1 -w 2000 1.1.1.1", 5_000);
            if (code != 0) return "✕";
            var m = Regex.Match(stdout, @"(?:temps|time)\s*[=<>]\s*(\d+)\s*ms", RegexOptions.IgnoreCase);
            return m.Success ? m.Groups[1].Value + "ms" : "?";
        }
        catch { return "?"; }
    }

    private void Overlay_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) Close();
        if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl))
            && e.Key == Key.C)
        {
            Clipboard.SetText($"{CpuText.Text} | {RamText.Text} | {PingText.Text}");
            e.Handled = true;
        }
    }

    private void Root_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
        SavePosition();
    }

    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void ClickThrough_Changed(object sender, RoutedEventArgs e)
    {
        try
        {
            var h = new WindowInteropHelper(this).Handle;
            if (h == IntPtr.Zero)
            {
                Logger.Warn("Overlay click-through: handle nul (fenêtre pas encore créée)");
                return;
            }
            const int GWL_EXSTYLE = -20;
            const int WS_EX_TRANSPARENT = 0x20;
            int style = GetWindowLong(h, GWL_EXSTYLE);
            bool on = ClickThroughBox.IsChecked == true;
            int next = on ? style | WS_EX_TRANSPARENT : style & ~WS_EX_TRANSPARENT;
            if (SetWindowLong(h, GWL_EXSTYLE, next) == 0)
                Logger.Warn("Overlay click-through: SetWindowLong a échoué");
            else
                SettingsService.Set("overlay_clickthrough", on ? "1" : "0");
        }
        catch (Exception ex) { Logger.Error("Overlay click-through", ex); }
    }

    private void SavePosition()
    {
        try
        {
            SettingsService.Set("overlay_x", Left.ToString(System.Globalization.CultureInfo.InvariantCulture));
            SettingsService.Set("overlay_y", Top.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception ex) { Logger.Warn($"Overlay position: {ex.Message}"); }
    }

    protected override void OnClosed(EventArgs e)
    {
        SavePosition();
        _timer.Stop();
        _monitor.Dispose();
        base.OnClosed(e);
    }

    // Note : Get/SetWindowLong suffit pour GWL_EXSTYLE (valeur 32-bit) même en x64.
    // SetLastError permet de détecter l'échec via le code retour 0.
    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);
}
