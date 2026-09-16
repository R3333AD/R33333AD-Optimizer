using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FPSBooster.App.Services;

namespace FPSBooster.App.Views;

public partial class LogWindow : Window
{
    private string _filter = "";
    private List<string> _lines = new();

    public LogWindow()
    {
        InitializeComponent();
        LoadLog();
    }

    private void LoadLog()
    {
        try
        {
            string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FPSBooster", "logs");
            string file = Path.Combine(dir, $"fpsbooster-{DateTime.Now:yyyyMMdd}.log");
            _lines = File.Exists(file) ? File.ReadAllLines(file).Reverse().Take(500).ToList() : new List<string> { "(aucun log aujourd'hui)" };
        }
        catch (Exception ex) { _lines = new List<string> { "Erreur lecture : " + ex.Message }; }
        ApplyFilter();
    }

    private void ApplyFilter()
    {
        LogList.Items.Clear();
        var shown = string.IsNullOrEmpty(_filter) ? _lines : _lines.Where(l => l.Contains(_filter)).ToList();
        foreach (var l in shown) LogList.Items.Add(l);
        LogCount.Text = $"{shown.Count} lignes";
        HighlightFilterButtons();
    }

    private void HighlightFilterButtons()
    {
        var def = new SolidColorBrush(Color.FromRgb(0x1C, 0x1C, 0x21));
        var acc = new SolidColorBrush(Color.FromRgb(0xC8, 0xFF, 0x2E));
        foreach (var (btn, tag) in new[] { (FilterAllBtn, ""), (FilterErrBtn, "ERROR"), (FilterWarnBtn, "WARN") })
        {
            bool active = _filter == tag;
            btn.Background = active ? acc : def;
            btn.Foreground = active ? Brushes.Black : Brushes.White;
        }
    }

    private void Filter_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button b) _filter = b.Tag as string ?? "";
        ApplyFilter();
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => LoadLog();

    private void Export_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            string src = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "FPSBooster");
            string dest = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Desktop), $"R33333AD-logs-{DateTime.Now:yyyyMMdd-HHmm}");
            Directory.CreateDirectory(dest);
            foreach (var f in Directory.EnumerateFiles(Path.Combine(src, "logs"), "fpsbooster-*.log"))
                File.Copy(f, Path.Combine(dest, Path.GetFileName(f)), overwrite: true);
            string journal = Path.Combine(src, "journal.jsonl");
            if (File.Exists(journal)) File.Copy(journal, Path.Combine(dest, "journal.jsonl"), overwrite: true);
            string settings = Path.Combine(src, "settings.txt");
            if (File.Exists(settings)) File.Copy(settings, Path.Combine(dest, "settings.txt"), overwrite: true);
            File.WriteAllText(Path.Combine(dest, "version.txt"), "R33333AD Optimizer — export assistance");
            System.Windows.MessageBox.Show($"Logs exportés vers :\n{dest}", "Export", MessageBoxButton.OK, MessageBoxImage.Information);
            Logger.Info($"Logs exportés: {dest}");
        }
        catch (Exception ex)
        {
            Logger.Error("Export logs", ex);
            System.Windows.MessageBox.Show("Export échoué : " + ex.Message, "Export", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
