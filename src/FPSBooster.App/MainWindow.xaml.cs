using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using FPSBooster.App.Models;
using FPSBooster.App.Services;
using FPSBooster.App.ViewModels;
using FPSBooster.App.Views;

namespace FPSBooster.App;

public partial class MainWindow : Window
{
    private readonly MainViewModel _vm;
    private readonly MonitoringService _monitor = new();
    private OverlayWindow? _overlay;
    private System.Windows.Forms.NotifyIcon? _tray;
    private bool _reallyExit;
    private bool _restoreDone;

    public MainWindow()
    {
        InitializeComponent();
        _vm = new MainViewModel();
        DataContext = _vm;
        ReportService.ToolSource = () => _vm.Tools;
        KeyDown += MainWindow_KeyDown;

        Loaded += MainWindow_Loaded;
        Closing += MainWindow_Closing;
        // Ne jamais bloquer l'arrêt/redémarrage Windows : quitter vraiment dans ce cas
        System.Windows.Application.Current.SessionEnding += (_, _) => _reallyExit = true;
        SetupTray();
        BgVideo.MediaEnded += (_, _) =>
        {
            BgVideo.Position = TimeSpan.Zero;
            BgVideo.Play();
        };
        BgVideo.MediaFailed += (_, e) =>
        {
            Logger.Error("BgVideo MediaFailed: " + (e.ErrorException?.Message ?? "Inconnu"));
            BgVideo.Source = null;
            BgVideo.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(300)));
        };
        ApplyDirection();
        Loc.Instance.LanguageChanged += () =>
        {
            ApplyDirection();
            RefreshLangPills();
            RefreshRevertBtn();
            if (_vm.AppsLoaded)
                UninstStatus.Text = Loc.Instance.Get("Un_Loaded", _vm.Apps.Count);
        };

        var view = CollectionViewSource.GetDefaultView(_vm.Tools);
        view.Filter = o =>
        {
            if (o is not ToolItem t) return false;
            if (t.Risk > _vm.MaxRisk) return false;
            if (_vm.SelectedSection.Id != "home" && !t.SectionIds.Contains(_vm.SelectedSection.Id))
                return false;
            if (string.IsNullOrWhiteSpace(_vm.Search)) return true;
            return t.Title.Contains(_vm.Search, StringComparison.OrdinalIgnoreCase);
        };
        _vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(MainViewModel.Search) ||
                e.PropertyName == nameof(MainViewModel.MaxRisk))
                CollectionViewSource.GetDefaultView(_vm.Tools).Refresh();
            if (e.PropertyName == nameof(MainViewModel.SelectedSection))
            {
                CollectionViewSource.GetDefaultView(_vm.Tools).Refresh();
                UpdateSectionVisibility();
            }
        };

        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
        timer.Tick += (_, _) =>
        {
            _vm.Cpu = Math.Round(_monitor.GetCpuPercent(), 0);
            _vm.Ram = Math.Round(_monitor.GetRamPercent(), 0);
            _vm.Gpu = _monitor.GetGpuText();
            CpuBar.Width = Math.Max(4, _vm.Cpu * 1.4);
            RamBar.Width = Math.Max(4, _vm.Ram * 1.4);
        };
        timer.Start();
    }

    private void MainWindow_Loaded(object sender, RoutedEventArgs e)
    {
        RefreshLangPills();
        RefreshRiskPills();
        RefreshThemeBtn();
        RefreshRevertBtn();
        try
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "logo.ico");
            if (File.Exists(iconPath))
                Icon = BitmapFrame.Create(new Uri(iconPath));
        }
        catch (Exception ex) { Logger.Error("Window icon", ex); }
        ApplyBackground();
        if (!AdminHelper.IsAdmin())
            ShowToast(Loc.Instance.Get("Msg_Admin"));
        if (SettingsService.Get("update_auto", "1") == "1")
            _ = AutoUpdateCheckAsync();

CpuBar.Width = 20;
            RamBar.Width = 20;
        UpdateSectionVisibility();
        AnimateCards();
        InitScheduler();
        RefreshBenchHistory();
        RefreshDiskHealth();
        InitUpdateSettings();
        _ = InitAutoStartAsync();
    }

    private void UpdateSectionVisibility()
    {
        bool isHome = _vm.SelectedSection.Id == "home";
        bool isUninst = _vm.SelectedSection.Id == "uninst";
        HomePanel.Visibility = isHome ? Visibility.Visible : Visibility.Collapsed;
        UninstallerPanel.Visibility = isUninst ? Visibility.Visible : Visibility.Collapsed;
        ToolsScroll.Visibility = (!isHome && !isUninst) ? Visibility.Visible : Visibility.Collapsed;
        ToolsHeaderActions.Visibility = ToolsScroll.Visibility;
    }

    private void AnimateCards()
    {
        if (ToolsScroll.Visibility != Visibility.Visible) return;
        // Laisse le layout se faire avant d'animer
        Dispatcher.BeginInvoke(new Action(() =>
        {
            var cards = FindVisualChildren<Border>(ToolsList)
                .Where(b => (string?)b.Tag == "ToolCard").ToList();
            for (int i = 0; i < cards.Count; i++)
            {
                var card = cards[i];
                card.Opacity = 0;
                card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
                {
                    BeginTime = TimeSpan.FromMilliseconds(60 + i * 35)
                });
            }
        }), DispatcherPriority.Loaded);
    }

    private void ApplyDirection()
    {
        FlowDirection = Loc.Instance.IsRtl ? FlowDirection.RightToLeft : FlowDirection.LeftToRight;
    }

    private void Lang_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string lang) return;
        Loc.Instance.Set(lang);
        RefreshLangPills();
        // petit slide animé
        btn.RenderTransform = new TranslateTransform();
        ((TranslateTransform)btn.RenderTransform).BeginAnimation(TranslateTransform.YProperty,
            new DoubleAnimation(0, -3, TimeSpan.FromMilliseconds(90)) { AutoReverse = true });
    }

    private void ThemeToggle_Click(object sender, RoutedEventArgs e)
    {
        bool dark = !SettingsService.GetDarkMode();
        SettingsService.SetDarkMode(dark);
        App.ApplyTheme();
        var btn = sender as Button;
        if (btn != null) btn.Content = dark ? "☀" : "☾";
    }

    private void RefreshThemeBtn()
    {
        try { ThemeBtn.Content = SettingsService.GetDarkMode() ? "☀" : "☾"; }
        catch (Exception ex) { Logger.Warn($"RefreshThemeBtn: {ex.Message}"); }
    }

    private void RefreshLangPills()
    {
        var pills = new[] { (LangFrBtn, "fr"), (LangArBtn, "ar"), (LangEnBtn, "en") };
        foreach (var (btn, lang) in pills)
        {
            bool active = Loc.Instance.Current == lang;
            btn.Background = active ? Brushes.White : Brushes.Transparent;
            btn.Foreground = active ? Brushes.Black : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#52525B"));
        }
    }

    private void Risk_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || !int.TryParse(btn.Tag as string, out int level)) return;
        _vm.SetRisk(level);
        RefreshRiskPills();
        AnimateCards();
        Logger.Info($"Niveau de risque: {level}");
    }

    private void RefreshRiskPills()
    {
        var pills = new[] { (Risk0Btn, 0), (Risk1Btn, 1), (Risk2Btn, 2) };
        foreach (var (btn, level) in pills)
        {
            bool active = _vm.MaxRisk == level;
            btn.Background = active ? new SolidColorBrush((Color)ColorConverter.ConvertFromString("#C8FF2E")) : Brushes.Transparent;
            btn.Foreground = active ? Brushes.Black : new SolidColorBrush((Color)ColorConverter.ConvertFromString("#52525B"));
        }
    }

    private void EnsureRestorePointOnce()
    {
        if (_restoreDone) return;
        _restoreDone = true;
        _vm.Status = "Point de restauration...";
        Task.Run(() => AdminHelper.CreateRestorePoint());
        ShowToast(Loc.Instance.Get("Msg_RestoreToast"));
    }

    private void MainWindow_KeyDown(object sender, KeyEventArgs e)
    {
        bool ctrl = Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl);
        bool shift = Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift);
        if (e.Key == Key.O && ctrl && shift)
        {
            if (_overlay != null) { _overlay.Close(); _overlay = null; }
            else { _overlay = new OverlayWindow(); _overlay.Closed += (_, _) => _overlay = null; _overlay.Show(); }
            e.Handled = true;
        }
        else if (e.Key == Key.B && ctrl && shift)
        {
            BoostAll_Click(sender, e);
            e.Handled = true;
        }
        else if (e.Key == Key.R && ctrl && shift)
        {
            Revert_Click(sender, e);
            e.Handled = true;
        }
    }
    private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2) { ToggleMax(); return; }
        if (e.LeftButton == MouseButtonState.Pressed) DragMove();
    }

    private void Min_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Max_Click(object sender, RoutedEventArgs e) => ToggleMax();
    private void Close_Click(object sender, RoutedEventArgs e) => Close();

    private void Journal_Click(object sender, RoutedEventArgs e)
    {
        var w = new JournalWindow(() => RefreshRevertBtn()) { Owner = this };
        w.Show();
        Logger.Info("Journal ouvert");
    }

    private async void Help_Click(object sender, RoutedEventArgs e)
    {
        string admin = AdminHelper.IsAdmin() ? "oui/yes" : "non/no";
        string msg = Loc.Instance.Get("Help_Msg", UpdateService.CurrentVersion, admin);
        try
        {
            var prereqs = await PrereqService.CheckAsync();
            msg += "\n\n" + Loc.Instance.Get("Help_Prereq") + "\n"
                + string.Join("\n", prereqs.Select(p => $"{(p.Ok ? "✓" : "✕")} {p.Name} — {p.Detail}"));
        }
        catch (Exception ex) { Logger.Warn($"Aide prérequis: {ex.Message}"); }
        MessageBox.Show(msg, Loc.Instance.Get("Help_Title"), MessageBoxButton.OK, MessageBoxImage.Information);
        Logger.Info("Aide ouverte");
    }

    // ---------------- ZONE DE NOTIFICATION ----------------

    private void SetupTray()
    {
        _tray = new System.Windows.Forms.NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "R33333AD Optimizer",
            Visible = false
        };
        _tray.DoubleClick += (_, _) => ShowFromTray();
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add(Loc.Instance.Get("Tray_Open"), null, (_, _) => ShowFromTray());
        menu.Items.Add(Loc.Instance.Get("Tray_Quit"), null, (_, _) => QuitApp());
        _tray.ContextMenuStrip = menu;
        // Texte du menu rafraîchi à chaque ouverture (suit la langue)
        _tray.ContextMenuStrip.Opening += (_, _) =>
        {
            _tray.ContextMenuStrip.Items[0].Text = Loc.Instance.Get("Tray_Open");
            _tray.ContextMenuStrip.Items[1].Text = Loc.Instance.Get("Tray_Quit");
        };
    }

    private static System.Drawing.Icon LoadAppIcon()
    {
        try
        {
            string p = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "logo.ico");
            if (File.Exists(p)) return new System.Drawing.Icon(p);
        }
        catch (Exception ex) { Logger.Error("Tray icon load", ex); }
        return System.Drawing.SystemIcons.Shield;
    }

    private void MainWindow_Closing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_reallyExit) return;
        e.Cancel = true;
        HideToTray();
    }

    private void HideToTray()
    {
        Hide();
        try { BgVideo.Pause(); } catch { }
        _kenBurns?.Pause();
        if (_tray != null)
        {
            _tray.Visible = true;
            _tray.ShowBalloonTip(3000, Loc.Instance.Get("Tray_MinTitle"), Loc.Instance.Get("Tray_MinMsg"),
                System.Windows.Forms.ToolTipIcon.Info);
        }
        Logger.Info("App réduite en zone de notification");
    }

    private void ShowFromTray()
    {
        Show();
        WindowState = WindowState.Normal;
        Activate();
        if (_bgIsVideo) { try { BgVideo.Play(); } catch { } }
        try { _kenBurns?.Resume(); } catch { }
        if (_tray != null) _tray.Visible = false;
    }

    private void QuitApp()
    {
        _reallyExit = true;
        try
        {
            if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        }
        catch { }
        try { _overlay?.Close(); } catch { }
        Logger.Info("App quittée via tray");
        System.Windows.Application.Current.Shutdown();
    }

    private void ToggleMax()
    {
        if (WindowState == WindowState.Maximized)
        {
            WindowState = WindowState.Normal;
            ClearValue(MaxHeightProperty);
            ClearValue(MaxWidthProperty);
            MaxBtn.Content = "☐";
            OuterBorder.CornerRadius = new CornerRadius(14);
        }
        else
        {
            // Limite à la zone de travail (ne recouvre pas la barre des tâches)
            MaxHeight = SystemParameters.WorkArea.Height;
            MaxWidth = SystemParameters.WorkArea.Width;
            WindowState = WindowState.Maximized;
            MaxBtn.Content = "❐";
            OuterBorder.CornerRadius = new CornerRadius(0);
        }
    }

    private void Nav_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button clicked) return;
        if (clicked.DataContext is not SectionItem section) return;

        _vm.SelectSection(section);

        // transition : fondu rapide du contenu + petit slide du bouton
        MainContent.BeginAnimation(OpacityProperty,
            new DoubleAnimation(0.3, 1, TimeSpan.FromMilliseconds(300)));
        clicked.RenderTransform = new TranslateTransform();
        ((TranslateTransform)clicked.RenderTransform).BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(0, 6, TimeSpan.FromMilliseconds(90)) { AutoReverse = true });

        if (section.Id == "uninst" && !_vm.AppsLoaded)
            _ = LoadAppsAsync();
        else
            AnimateCards();
        if (section.Id == "home") { RefreshBenchHistory(); RefreshDiskHealth(); }
    }

    private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        CollectionViewSource.GetDefaultView(_vm.Tools).Refresh();
    }

    // ---------------- DÉSINSTALLEUR ----------------

    private bool _appsLoading;

    private async Task LoadAppsAsync()
    {
        if (_appsLoading) return;
        _appsLoading = true;
        try
        {
            UninstStatus.Text = Loc.Instance.Get("Un_Loading");
        _vm.Apps.Clear();
        var apps = await Task.Run(UninstallerService.GetInstalledApps);
        foreach (var a in apps) _vm.Apps.Add(a);
        _vm.AppsLoaded = true;
        var view = CollectionViewSource.GetDefaultView(_vm.Apps);
        view.Filter = o =>
        {
            if (o is not InstalledApp a) return false;
            if (string.IsNullOrWhiteSpace(_vm.AppSearch)) return true;
            return a.DisplayName.Contains(_vm.AppSearch, StringComparison.OrdinalIgnoreCase) ||
                   (a.Publisher?.Contains(_vm.AppSearch, StringComparison.OrdinalIgnoreCase) == true);
        };
        _vm.RefreshSubtitle();
        _vm.PruneQueue();
        RefreshQueueBtn();
        UninstStatus.Text = Loc.Instance.Get("Un_Loaded", apps.Count);
        }
        finally { _appsLoading = false; }
    }

    private string _appsSortCol = "";
    private ListSortDirection _appsSortDir = ListSortDirection.Ascending;

    private void SortApps_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            if (e.OriginalSource is not GridViewColumnHeader h) return;
            string col = UiSortHelper.ResolveSortColumn((h.Column.DisplayMemberBinding as Binding)?.Path.Path);
            _appsSortDir = UiSortHelper.NextDirection(col, _appsSortCol, _appsSortDir);
            _appsSortCol = col;
            var view = CollectionViewSource.GetDefaultView(_vm.Apps);
            view.SortDescriptions.Clear();
            view.SortDescriptions.Add(new SortDescription(col, _appsSortDir));
            Logger.Info($"Tri apps: {col} {(_appsSortDir == ListSortDirection.Ascending ? "↑" : "↓")}");
        }
        catch (Exception ex) { Logger.Warn($"Tri apps: {ex.Message}"); }
    }

    private void QueueCheck_Changed(object sender, RoutedEventArgs e)
    {
        if ((sender as CheckBox)?.Tag is InstalledApp app)
        {
            _vm.ToggleQueue(app);
            RefreshQueueBtn();
        }
    }

    private void RefreshQueueBtn()
    {
        int n = _vm.QueueKeys.Count;
        QueueBtn.Content = Loc.Instance.Get("Un_UninstallQueue", n);
        QueueBtn.IsEnabled = n > 0;
    }

    private async void UninstallQueue_Click(object sender, RoutedEventArgs e)
    {
        var queued = _vm.Apps.Where(a => _vm.IsQueued(a)).ToList();
        if (queued.Count == 0)
        {
            UninstStatus.Text = Loc.Instance.Get("Un_QueueEmpty");
            return;
        }
        if (!AdminHelper.IsAdmin()) { ShowToast(Loc.Instance.Get("Msg_Admin")); return; }
        string names = string.Join("\n• ", queued.Take(10).Select(a => a.DisplayName))
            + (queued.Count > 10 ? $"\n… +{queued.Count - 10}" : "");
        var confirm = MessageBox.Show(
            Loc.Instance.Get("Dlg_QueueMsg", queued.Count, names),
            Loc.Instance.Get("Dlg_QueueTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        EnsureRestorePointOnce();
        UninstallBtn.IsEnabled = false;
        QueueBtn.IsEnabled = false;
        _vm.Leftovers.Clear();
        int ok = 0, fail = 0, i = 0;
        try
        {
            foreach (var app in queued)
            {
                i++;
                UninstStatus.Text = $"{Loc.Instance.Get("Un_Uninstalling", app.DisplayName)} ({i}/{queued.Count})";
                var (s, msg) = await UninstallerService.UninstallAsync(app);
                if (s) ok++; else fail++;
                Logger.Info($"File {i}/{queued.Count} {app.DisplayName} -> {(s ? "OK" : "FAIL")} : {msg}");
                var leftovers = await Task.Run(() => UninstallerService.ScanLeftovers(app));
                foreach (var l in leftovers)
                    _vm.Leftovers.Add(new LeftoverItem { Source = l });
            }
            _vm.ClearQueue();
            RefreshQueueBtn();
            _vm.AppsLoaded = false;
            await LoadAppsAsync();
            UninstStatus.Text = Loc.Instance.Get("Un_QueueDone", ok, fail);
            ShowToast($"🗑 {Loc.Instance.Get("Un_QueueDone", ok, fail)}");
        }
        finally
        {
            UninstallBtn.IsEnabled = true;
            RefreshQueueBtn();
        }
    }

    private async void RefreshApps_Click(object sender, RoutedEventArgs e)
    {
        _vm.AppsLoaded = false;
        _vm.Leftovers.Clear();
        await LoadAppsAsync();
    }

    private void AppSearchBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        CollectionViewSource.GetDefaultView(_vm.Apps).Refresh();
    }

    private async void Uninstall_Click(object sender, RoutedEventArgs e)
    {
        var app = _vm.SelectedApp;
        if (app == null)
        {
            UninstStatus.Text = Loc.Instance.Get("Un_PickFirst");
            return;
        }
        var confirm = MessageBox.Show(
            Loc.Instance.Get("Dlg_UnMsg", app.DisplayName, app.Version ?? "—", app.Publisher ?? "—"),
            Loc.Instance.Get("Dlg_UnTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        EnsureRestorePointOnce();
        UninstallBtn.IsEnabled = false;
        _vm.Leftovers.Clear();
        try
        {
            UninstStatus.Text = Loc.Instance.Get("Un_Uninstalling", app.DisplayName);
            var (ok, msg) = await UninstallerService.UninstallAsync(app);
            ShowToast((ok ? "✓ " : "✕ ") + msg);

            UninstStatus.Text = Loc.Instance.Get("Un_Scanning");
            var leftovers = await Task.Run(() => UninstallerService.ScanLeftovers(app));
            foreach (var l in leftovers)
                _vm.Leftovers.Add(new LeftoverItem { Source = l });
            UninstStatus.Text = leftovers.Count == 0
                ? Loc.Instance.Get("Un_Clean")
                : Loc.Instance.Get("Un_Found", leftovers.Count);

            // Rafraîchit la liste (le programme a normalement disparu)
            _vm.AppsLoaded = false;
            await LoadAppsAsync();
        }
        finally { UninstallBtn.IsEnabled = true; }
    }

    private async void DeleteLeftovers_Click(object sender, RoutedEventArgs e)
    {
        if (!AdminHelper.IsAdmin()) { ShowToast("⚠ Admin requis pour supprimer les restes"); return; }
        var selected = _vm.Leftovers.Where(l => l.IsSelected).Select(l => l.Source).ToList();
        if (selected.Count == 0)
        {
            UninstStatus.Text = Loc.Instance.Get("Un_NothingChecked");
            return;
        }
        var confirm = MessageBox.Show(
            Loc.Instance.Get("Dlg_DelMsg", selected.Count),
            Loc.Instance.Get("Dlg_DelTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        var (deleted, failed) = await Task.Run(() => UninstallerService.DeleteLeftovers(selected));
        foreach (var done in _vm.Leftovers.Where(l => l.IsSelected).ToList())
            _vm.Leftovers.Remove(done);
        UninstStatus.Text = Loc.Instance.Get("Un_Deleted", deleted, failed);
        ShowToast($"🗑 {deleted} restes supprimés / {failed} échecs");
    }

    private async Task RunToolAsync(ToolItem item)
    {
        if (item.IsRunning) return;
        EnsureRestorePointOnce();
        _vm.Status = $"Exécution : {item.Title}...";

        var result = await ToolRunner.RunAsync(item);

        // LEDs des sous-outils : profils (réussites réelles) + packs (inclus connus) + gain mesuré du profil
        if (result.Success)
        {
            IEnumerable<string> subs = GameProfiles.ById(item.ActionId) != null
                ? TweakLibrary.LastProfileSucceeded
                : TweakLibrary.IncludedOf(item.ActionId);
            foreach (var sub in subs)
            {
                var tool = _vm.FindTool(sub);
                if (tool != null) tool.IsApplied = true;
            }
            if (GameProfiles.ById(item.ActionId) != null)
                FpsGainService.ApplyMeasured(item);
        }

        _vm.Executed = _vm.Tools.Count(t => t.IsApplied);
        _vm.Status = result.Message;
        ShowToast((result.Success ? "✓ " : "✕ ") + result.Message);
        AnimateProgress(result.Success ? 100 : RunProgress.Value);
        RefreshRevertBtn();
        if (item.ActionId == "benchmark") RefreshBenchHistory();
    }

    private async void Card_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Border border) return;
        if (border.DataContext is not ToolItem item) return;
        border.IsHitTestVisible = false;
        try { await RunToolAsync(item); }
        finally { border.IsHitTestVisible = true; }
    }

    private bool _sortByGain;

    private void SortGain_Click(object sender, RoutedEventArgs e)
    {
        _sortByGain = !_sortByGain;
        var view = CollectionViewSource.GetDefaultView(_vm.Tools);
        view.SortDescriptions.Clear();
        if (_sortByGain)
            view.SortDescriptions.Add(new SortDescription(nameof(ToolItem.GainSort), ListSortDirection.Descending));
        SortGainBtn.Content = _sortByGain ? "⇅ % ✓" : "⇅ %";
        AnimateCards();
        Logger.Info($"Tri par gain: {(_sortByGain ? "ON" : "OFF")}");
    }

    private async void MeasureGain_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn) return;
        if (btn.DataContext is not ToolItem item) return;
        if (item.IsRunning) return;
        if (item.GainSort < 0)
        {
            ShowToast(Loc.Instance.Get("Gain_NotMeasurable"));
            return;
        }
        var def = TweakLibrary.ById(item.ActionId);
        if (def == null) return;
        if (!AdminHelper.IsAdmin()) { ShowToast(Loc.Instance.Get("Msg_Admin")); return; }
        var confirm = MessageBox.Show(
            Loc.Instance.Get("Gain_Msg", item.Title),
            Loc.Instance.Get("Gain_Title"), MessageBoxButton.YesNo, MessageBoxImage.Question);
        if (confirm != MessageBoxResult.Yes) return;

        EnsureRestorePointOnce();
        btn.IsEnabled = false;
        item.IsRunning = true;
        _vm.Status = Loc.Instance.Get("Gain_Measuring");
        if (GameProfiles.ById(item.ActionId) != null)
        {
            // Le profil mesure déjà avant/après en interne : exécution simple + LEDs + gain mesuré
            try
            {
                var res = await ToolRunner.RunAsync(item);
                if (res.Success)
                {
                    foreach (var sub in TweakLibrary.LastProfileSucceeded)
                    {
                        var tool = _vm.FindTool(sub);
                        if (tool != null) tool.IsApplied = true;
                    }
                    FpsGainService.ApplyMeasured(item);
                }
                _vm.Executed = _vm.Tools.Count(t => t.IsApplied);
                _vm.Status = res.Message;
                ShowToast((res.Success ? "✓ " : "✕ ") + res.Message);
                AnimateProgress(res.Success ? 100 : RunProgress.Value);
                RefreshRevertBtn();
            }
            finally
            {
                item.IsRunning = false;
                btn.IsEnabled = true;
            }
            return;
        }
        try
        {
            var progress = new Progress<double>(v => RunProgress.Value = v);
            var (ok, gain, msg) = await FpsGainService.MeasureGainAsync(item.ActionId, def.Run, progress);
            if (ok)
            {
                item.GainText = FpsGainService.FormatMeasured(gain);
                item.GainSort = gain;
                item.IsApplied = true;
                _vm.Executed = _vm.Tools.Count(t => t.IsApplied);
                _vm.Status = Loc.Instance.Get("Gain_Measured", gain);
                ShowToast("✓ " + _vm.Status + " — " + msg);
                AnimateProgress(100);
                RefreshRevertBtn();
            }
            else
            {
                _vm.Status = msg;
                ShowToast("✕ " + msg);
            }
        }
        finally
        {
            item.IsRunning = false;
            btn.IsEnabled = true;
        }
    }

    private async void HomeQuick_Click(object sender, RoutedEventArgs e)
    {
        if (!AdminHelper.IsAdmin()) { ShowToast("⚠ Admin requis pour cette action"); return; }
        if (sender is not Button btn) return;
        if (btn.Tag is not string actionId) return;
        var item = _vm.FindTool(actionId);
        if (item == null) return;
        btn.IsEnabled = false;
        try { await RunToolAsync(item); }
        finally { btn.IsEnabled = true; }
    }

    private void OverlayToggle_Click(object sender, RoutedEventArgs e)
    {
        if (_overlay != null)
        {
            _overlay.Close();
            _overlay = null;
            return;
        }
        _overlay = new OverlayWindow();
        _overlay.Closed += (_, _) => _overlay = null;
        _overlay.Show();
        Logger.Info("Overlay HUD ouvert");
    }

    private List<ToolItem> VisibleTools() =>
        CollectionViewSource.GetDefaultView(_vm.Tools).Cast<ToolItem>().ToList();

    private async void BoostAll_Click(object sender, RoutedEventArgs e)
    {
        if (!AdminHelper.IsAdmin()) { ShowToast(Loc.Instance.Get("Msg_Admin")); return; }
        var targets = VisibleTools();
        if (targets.Count == 0)
        {
            ShowToast(Loc.Instance.Get("Sub_Tools", 0));
            return;
        }
        string badges = string.Join(" • ",
            targets.GroupBy(t => t.Badge).OrderBy(g => g.Key).Select(g => $"{g.Key} : {g.Count()}"));
        string risks = string.Join(" • ",
            targets.GroupBy(t => t.Risk).OrderBy(g => g.Key).Select(g => $"{Loc.Instance.Get($"Risk_{g.Key}")} ×{g.Count()}"));
        string names = string.Join("\n• ", targets.Take(12).Select(t => t.Title))
            + (targets.Count > 12 ? "\n" + Loc.Instance.Get("Boost_More", targets.Count - 12) : "");
        var confirm = MessageBox.Show(
            Loc.Instance.Get("Dlg_BoostAllMsg", _vm.SelectedSection.Title, targets.Count, badges, risks, names),
            Loc.Instance.Get("Dlg_BoostAllTitle"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        EnsureRestorePointOnce();
        BoostAllBtn.IsEnabled = false;
        _vm.Status = $"BOOST en cours ({targets.Count} outils)...";

        var progress = new Progress<double>(v => RunProgress.Value = v);
        var (ok, fail) = await ToolRunner.RunAllAsync(targets, progress);

        _vm.Executed = _vm.Tools.Count(t => t.IsApplied);
        ShowToast($"⚡ BOOST : {ok} OK / {fail} échecs ({_vm.SelectedSection.Title})");
        _vm.Status = $"Terminé : {ok} OK / {fail} échecs";
        BoostAllBtn.IsEnabled = true;
        RefreshRevertBtn();
    }

    private void RefreshRevertBtn()
    {
        int n = TweakJournal.Count();
        RevertBtn.Content = $"{Loc.Instance.Get("Undo_Btn")} ({n})";
        RevertBtn.IsEnabled = n > 0;
    }

    private void RefreshBenchHistory()
    {
        try
        {
            var hist = BenchmarkService.LoadHistory().TakeLast(8).ToList();            BenchHistList.ItemsSource = hist.Count == 0
                ? new List<string> { Loc.Instance.Get("Bench_Empty") }
                : hist
                .Select(h => $"{h.Date[..10]} — {h.Score}/100 (CPU {h.CpuMs:0}ms / RAM {h.RamMs:0}ms / disque {h.DiskMs:0}ms)")
                .ToList();
            var pts = new PointCollection();
            if (hist.Count >= 2)
            {
                double max = hist.Max(h => h.Score), min = hist.Min(h => h.Score);
                double span = Math.Max(1, max - min);
                for (int i = 0; i < hist.Count; i++)
                    pts.Add(new Point(i * (300.0 / Math.Max(1, hist.Count - 1)), 36 - (hist[i].Score - min) / span * 32));
            }
            BenchSpark.Points = pts;
        }
        catch (Exception ex) { Logger.Warn($"BenchHistory: {ex.Message}"); }
    }

    private void RefreshDiskHealth()
    {
        try
        {
            DiskList.ItemsSource = DiskHealthService.GetCached()
                .Take(3)
                .Select(d => new
                {
                    Icon = DiskHealthService.IconOf(d.Health),
                    Name = d.Name.Length > 34 ? d.Name[..34] + "…" : d.Name,
                    Health = d.Health
                })
                .ToList();
        }
        catch (Exception ex) { Logger.Warn($"DiskHealth: {ex.Message}"); }
    }

    // ---------------- MISES À JOUR ----------------

    private bool _updateUiReady;

    private void InitUpdateSettings()
    {
        try
        {
            UpdateUrlBox.Text = SettingsService.Get("update_url", UpdateService.DefaultUpdateUrl);
            UpdateAutoCheck.IsChecked = SettingsService.Get("update_auto", "1") == "1";
        }
        catch (Exception ex) { Logger.Warn($"Update settings init: {ex.Message}"); }
        finally { _updateUiReady = true; }
    }

    private void UpdateAuto_Changed(object sender, RoutedEventArgs e)
    {
        if (!_updateUiReady) return;
        SettingsService.Set("update_auto", UpdateAutoCheck.IsChecked == true ? "1" : "0");
    }

    private void UpdateUrl_Changed(object sender, TextChangedEventArgs e)
    {
        if (!_updateUiReady) return;
        SettingsService.Set("update_url", (UpdateUrlBox.Text ?? "").Trim());
    }

    private async void UpdateCheck_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var (found, msg) = await UpdateService.CheckAsync(download: false);
            string first = msg.Split('\n')[0];
            if (!found)
            {
                HideUpdatePill();
                ShowToast(first);
                return;
            }
            ShowToast("⬆ " + first);
            ShowUpdatePill(first, msg);
        }
        catch (Exception ex)
        {
            Logger.Error("Update check manuel", ex);
            ShowToast("Échec : " + ex.Message);
        }
    }

    // ---------------- DÉMARRAGE AUTO ----------------

    private bool _autoStartReady;

    private async Task InitAutoStartAsync()
    {
        try
        {
            AutoStartCheck.IsChecked = await StartupService.IsAutoStartAsync();
        }
        catch (Exception ex) { Logger.Warn($"AutoStart init: {ex.Message}"); }
        finally { _autoStartReady = true; }
    }

    private async void AutoStart_Changed(object sender, RoutedEventArgs e)
    {
        if (!_autoStartReady) return;
        bool on = AutoStartCheck.IsChecked == true;
        bool ok = await StartupService.SetAutoStartAsync(on);
        ShowToast(ok ? Loc.Instance.Get(on ? "Startup_On" : "Startup_Off")
                     : Loc.Instance.Get("Startup_Fail"));
        if (!ok)
        {
            _autoStartReady = false;
            AutoStartCheck.IsChecked = !on;
            _autoStartReady = true;
        }
    }

    // ---------------- PLANIFICATEUR ----------------
    private static readonly (string Id, string Title)[] SchedActions =
    {
        ("clean_temp", "🧹 Nettoyage"), ("flush_dns", "Flush DNS"),
        ("empty_recycle", "Corbeille"), ("trim_ram", "Trim RAM"), ("clean_logs", "Logs"),
    };

    private DispatcherTimer? _schedTimer;

    private void InitScheduler()
    {
        try
        {
            SchedAction.ItemsSource = SchedActions
                .Select(a => new ComboBoxItem { Content = a.Title, Tag = a.Id }).ToList();
            string saved = SettingsService.Get("sched_action", "clean_temp");
            SchedAction.SelectedIndex = Math.Max(0, Array.FindIndex(SchedActions, a => a.Id == saved));
            SchedTime.Text = SettingsService.Get("sched_time", "03:00");
            SchedCheck.IsChecked = SettingsService.Get("sched_on", "0") == "1";
            _schedTimer = new DispatcherTimer { Interval = TimeSpan.FromMinutes(1) };
            _schedTimer.Tick += async (_, _) => await SchedTickAsync();
            _schedTimer.Start();
        }
        catch (Exception ex) { Logger.Warn($"Planificateur init: {ex.Message}"); }
    }

    private void SaveSchedSettings()
    {
        try
        {
            SettingsService.Set("sched_on", SchedCheck.IsChecked == true ? "1" : "0");
            if (SchedAction.SelectedItem is ComboBoxItem ci && ci.Tag is string id)
                SettingsService.Set("sched_action", id);
            SettingsService.Set("sched_time", (SchedTime.Text ?? "").Trim());
        }
        catch (Exception ex) { Logger.Warn($"Planificateur save: {ex.Message}"); }
    }

    private void SchedCheck_Changed(object sender, RoutedEventArgs e) => SaveSchedSettings();
    private void SchedAction_Changed(object sender, SelectionChangedEventArgs e) => SaveSchedSettings();
    private void SchedTime_Changed(object sender, TextChangedEventArgs e) => SaveSchedSettings();

    private async Task SchedTickAsync()
    {
        try
        {
            if (SettingsService.Get("sched_on", "0") != "1") return;
            if (!TimeSpan.TryParse(SettingsService.Get("sched_time", "03:00"), out var at)) return;
            var now = DateTime.Now;
            if (now.TimeOfDay < at) return;
            string today = now.ToString("yyyyMMdd");
            if (SettingsService.Get("sched_last", "") == today) return; // déjà fait aujourd'hui
            SettingsService.Set("sched_last", today);
            string action = SettingsService.Get("sched_action", "clean_temp");
            var item = _vm.FindTool(action);
            if (item == null) return;
            Logger.Info($"Planificateur: exécution {action}");
            await RunToolAsync(item);
        }
        catch (Exception ex) { Logger.Error("Planificateur", ex); }
    }

    private async void Revert_Click(object sender, RoutedEventArgs e)
    {
        if (!AdminHelper.IsAdmin()) { ShowToast("⚠ Admin requis pour annuler"); return; }
        int n = TweakJournal.Count();
        if (n == 0) return;
        var confirm = MessageBox.Show(
            Loc.Instance.Get("Undo_Msg", n),
            Loc.Instance.Get("Undo_Title"), MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (confirm != MessageBoxResult.Yes) return;

        RevertBtn.IsEnabled = false;
        _vm.Status = "Annulation en cours...";
        var (ok, fail) = await Task.Run(RevertService.RevertAllAsync);
        foreach (var t in _vm.Tools) t.IsApplied = false;
        _vm.Executed = 0;
        _vm.Status = Loc.Instance.Get("Undo_Done", ok, fail);
        ShowToast(Loc.Instance.Get("Undo_Done", ok, fail));
        RefreshRevertBtn();
    }

    // ---------------- FOND PHOTO ----------------

    private Storyboard? _kenBurns;

    private bool _bgIsVideo;

    private void ApplyBackground()
    {
        // 1. Vidéo intégrée (Assets/bg.mp4) 2. Image intégrée 3. Ancien fichier perso 4. Rien
        string videoPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "bg.mp4");
        string embedded = "pack://application:,,,/Assets/bg.jpg";
        string filePath = SettingsService.Get("bg", "");
        bool useVideo = File.Exists(videoPath);
        bool useEmbedded = false;
        if (!useVideo)
        {
            try { useEmbedded = Application.GetResourceStream(new Uri(embedded)) != null; }
            catch { }
        }
        bool useFile = !useVideo && !useEmbedded && !string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath);
        _bgIsVideo = useVideo;
        BgVideo.Visibility = useVideo ? Visibility.Visible : Visibility.Collapsed;
        if (!useVideo && !useEmbedded && !useFile)
        {
            _kenBurns?.Stop();
            try { BgVideo.Stop(); } catch { }
            BgVideo.Source = null;
            BgImage.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(400)));
            BgOverlay.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(400)));
            return;
        }
        try
        {
            double v = BgOpacityValue();
            BgOpacitySlider.Value = v * 100;
            if (useVideo)
            {
                BgImage.Source = null;
                _kenBurns?.Stop();
                BgVideo.Source = new Uri(videoPath);
                BgVideo.Play();
                BgVideo.BeginAnimation(OpacityProperty, new DoubleAnimation(v, TimeSpan.FromMilliseconds(600)));
            }
            else
            {
                try { BgVideo.Stop(); } catch { }
                BgVideo.Source = null;
                var bmp = new BitmapImage();
                bmp.BeginInit();
                if (useEmbedded)
                    bmp.UriSource = new Uri(embedded);
                else
                {
                    bmp.UriSource = new Uri(filePath);
                    bmp.CacheOption = BitmapCacheOption.OnLoad;
                }
                bmp.EndInit();
                bmp.Freeze();
                BgImage.Source = bmp;
                BgImage.BeginAnimation(OpacityProperty, new DoubleAnimation(v, TimeSpan.FromMilliseconds(600)));
                StartKenBurns();
            }
            BgOverlay.BeginAnimation(OpacityProperty, new DoubleAnimation(OverlayFor(v), TimeSpan.FromMilliseconds(600)));
            Logger.Info($"Fond appliqué: {(useVideo ? "vidéo intégrée" : useEmbedded ? "image intégrée" : filePath)}");
        }
        catch (Exception ex)
        {
            Logger.Error("ApplyBackground", ex);
            SettingsService.Set("bg", "");
        }
    }

    private void StartKenBurns()
    {
        _kenBurns?.Stop();
        var scale = (ScaleTransform)BgImage.RenderTransform;
        var sb = new Storyboard { RepeatBehavior = RepeatBehavior.Forever, AutoReverse = true };
        var ax = new DoubleAnimation(1.0, 1.1, TimeSpan.FromSeconds(24));
        var ay = new DoubleAnimation(1.0, 1.1, TimeSpan.FromSeconds(24));
        Storyboard.SetTarget(ax, scale);
        Storyboard.SetTargetProperty(ax, new PropertyPath(ScaleTransform.ScaleXProperty));
        Storyboard.SetTarget(ay, scale);
        Storyboard.SetTargetProperty(ay, new PropertyPath(ScaleTransform.ScaleYProperty));
        sb.Children.Add(ax);
        sb.Children.Add(ay);
        _kenBurns = sb;
        sb.Begin();
    }

    private static double BgOpacityValue()
    {
        if (double.TryParse(SettingsService.Get("bgopacity", "35"),
                System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double v))
            return Math.Clamp(v / 100.0, 0.05, 0.6);
        return 0.35;
    }

    private static double OverlayFor(double v) => Math.Max(0.2, 0.8 - v);

    private void BgOpacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        double v = Math.Clamp(e.NewValue / 100.0, 0.05, 0.6);
        if (_bgIsVideo)
        {
            if (BgVideo.Source == null) return;
            BgVideo.BeginAnimation(OpacityProperty, null);
            BgVideo.Opacity = v;
        }
        else
        {
            if (BgImage.Source == null) return;
            BgImage.BeginAnimation(OpacityProperty, null);
            BgImage.Opacity = v;
        }
        BgOverlay.BeginAnimation(OpacityProperty, null);
        BgOverlay.Opacity = OverlayFor(v);
        SettingsService.Set("bgopacity", ((int)e.NewValue).ToString());
    }

    private async Task AutoUpdateCheckAsync()
    {
        try
        {
            await Task.Delay(5000); // laisse l'app démarrer
            var (found, msg) = await UpdateService.CheckAsync(download: false);
            if (!found) return;
            string first = msg.Split('\n')[0];
            ShowToast("⬆ " + first);
            ShowUpdatePill(first, msg);
            Logger.Info("Pastille màj affichée: " + first);
        }
        catch (Exception ex) { Logger.Error("AutoUpdate", ex); }
    }

    private bool _updateDismissed;

    private void ShowUpdatePill(string firstLine, string fullMsg)
    {
        if (_updateDismissed) return;
        PillVersionText.Text = firstLine;
        UpdatePill.ToolTip = fullMsg;
        UpdatePill.Visibility = Visibility.Visible;
        try
        {
            ((Storyboard)UpdatePill.FindResource("PillEnter")).Begin(UpdatePill, true);
            ((Storyboard)UpdatePill.FindResource("PillPulse")).Begin(UpdatePill, true);
        }
        catch (Exception ex) { Logger.Warn($"Pill anim: {ex.Message}"); }
    }

    private void HideUpdatePill()
    {
        try
        {
            ((Storyboard)UpdatePill.FindResource("PillEnter")).Stop(UpdatePill);
            ((Storyboard)UpdatePill.FindResource("PillPulse")).Stop(UpdatePill);
        }
        catch { }
        UpdatePill.Visibility = Visibility.Collapsed;
    }

    private void UpdatePill_Dismiss(object sender, RoutedEventArgs e)
    {
        _updateDismissed = true;
        HideUpdatePill();
        e.Handled = true;
        Logger.Info("Pastille màj ignorée pour cette session");
    }

    private async void UpdatePill_Click(object sender, MouseButtonEventArgs e)
    {
        UpdatePill.IsEnabled = false;
        try
        {
            var (found, msg) = await UpdateService.CheckAsync(download: true);
            if (!found)
            {
                ShowToast(msg.Split('\n')[0]);
                HideUpdatePill();
                return;
            }
            var confirm = MessageBox.Show(
                Loc.Instance.Get("Update_InstallMsg", msg.Split('\n')[0]),
                Loc.Instance.Get("Update_InstallTitle"), MessageBoxButton.YesNo, MessageBoxImage.Question);
            if (confirm != MessageBoxResult.Yes) return;
            string? installer = Directory.GetFiles(
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads"),
                    "Setup-R33333ADOptimizer-*.exe")
                .OrderByDescending(File.GetCreationTime).FirstOrDefault();
            if (installer == null)
            {
                ShowToast(Loc.Instance.Get("Update_NoFile"));
                return;
            }
            Logger.Info($"Lancement installeur màj: {installer} + fermeture app");
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo { FileName = installer, UseShellExecute = true });
            System.Windows.Application.Current.Shutdown();
        }
        catch (Exception ex)
        {
            Logger.Error("Update install", ex);
            ShowToast("Échec : " + ex.Message);
        }
        finally { UpdatePill.IsEnabled = true; }
    }

    private void ShowToast(string msg)
    {
        ToastText.Text = msg;
        var trans = (TranslateTransform)Toast.RenderTransform;
        trans.X = 60;
        Toast.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(250)));
        trans.BeginAnimation(TranslateTransform.XProperty,
            new DoubleAnimation(60, 0, TimeSpan.FromMilliseconds(320))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
        var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(2.5) };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Toast.BeginAnimation(OpacityProperty, new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(400)));
            trans.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(0, 40, TimeSpan.FromMilliseconds(400)));
        };
        timer.Start();
    }

    private void AnimateProgress(double to)
    {
        RunProgress.BeginAnimation(ProgressBar.ValueProperty,
            new DoubleAnimation(RunProgress.Value, to, TimeSpan.FromMilliseconds(500))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
            });
    }

    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    {
        if (parent == null) yield break;
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is T t) yield return t;
            foreach (var c in FindVisualChildren<T>(child)) yield return c;
        }
    }
}
