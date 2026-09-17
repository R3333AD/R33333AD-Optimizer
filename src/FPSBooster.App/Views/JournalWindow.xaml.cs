using System.Windows;
using System.Windows.Controls;
using FPSBooster.App.Models;
using FPSBooster.App.Services;

namespace FPSBooster.App.Views;

public partial class JournalWindow : Window
{
    private readonly Action? _onChanged;

    private record Row(JournalEntry E, string When, string Action, string Detail);

    public JournalWindow(Action? onChanged = null)
    {
        InitializeComponent();
        _onChanged = onChanged;
        Title = Loc.Instance.Get("Journal_Title");
        LoadEntries();
    }

    private void LoadEntries()
    {
        try
        {
            var list = TweakJournal.Load();
            list.Reverse();
            EntriesList.ItemsSource = list.Select(e => new Row(
                e,
                e.Time.Length >= 16 ? e.Time[..16].Replace('T', ' ') : e.Time,
                string.IsNullOrEmpty(e.ActionId)
                    ? Loc.Instance.Get("Journal_ActUnknown")
                    : TweakLibrary.ById(e.ActionId)?.Title ?? e.ActionId,
                e.Area == "reg" ? $"{e.Hive}\\{e.Key} :: {e.Name}"
                : e.Area == "service" ? $"service {e.Hive}"
                : e.Area == "power" ? $"plan {e.OldValue}"
                : e.Area)).ToList();
            JournalStatus.Text = list.Count == 0
                ? Loc.Instance.Get("Journal_Empty")
                : Loc.Instance.Get("Journal_N", list.Count);
        }
        catch (Exception ex)
        {
            Logger.Error("JournalWindow load", ex);
            JournalStatus.Text = ex.Message;
        }
    }

    private async void RevertOne_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as Button)?.Tag is not JournalEntry entry) return;
        if (sender is Button btn) btn.IsEnabled = false;
        try
        {
            bool ok = await RevertService.RevertOneAsync(entry);
            JournalStatus.Text = ok
                ? Loc.Instance.Get("Journal_Reverted", entry.Name)
                : Loc.Instance.Get("Journal_Failed", entry.Name);
            if (ok)
            {
                LoadEntries();
                _onChanged?.Invoke();
            }
        }
        finally { if (sender is Button b) b.IsEnabled = true; }
    }

    private void Refresh_Click(object sender, RoutedEventArgs e) => LoadEntries();
}
