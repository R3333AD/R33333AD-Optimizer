using System.Collections.ObjectModel;
using System.ComponentModel;
using FPSBooster.App.Models;
using FPSBooster.App.Services;

namespace FPSBooster.App.ViewModels;

public class MainViewModel : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<SectionItem> Sections { get; } = new()
    {
        new() { Id = "home", Number = "⌂", LocKey = "Nav_Home", Header = "OVERVIEW", Subtitle = "État du PC • actions rapides" },
        new() { Id = "clean", Number = "1", LocKey = "Nav_Clean", Header = "SECTION 1", Subtitle = "Nettoyage système" },
        new() { Id = "fps", Number = "2", LocKey = "Nav_Fps", Header = "SECTION 2", Subtitle = "Optimisations FPS" },
        new() { Id = "input", Number = "3", LocKey = "Nav_Input", Header = "SECTION 3", Subtitle = "Latence souris, clavier, réseau" },
        new() { Id = "kbd", Number = "4", LocKey = "Nav_Kbd", Header = "SECTION 4", Subtitle = "Souris et clavier" },
        new() { Id = "ram", Number = "5", LocKey = "Nav_Ram", Header = "SECTION 5", Subtitle = "Mémoire vive" },
        new() { Id = "reg", Number = "6", LocKey = "Nav_Reg", Header = "SECTION 6", Subtitle = "Tweaks registre" },
        new() { Id = "svc", Number = "7", LocKey = "Nav_Svc", Header = "SECTION 7", Subtitle = "Services Windows" },
        new() { Id = "tools", Number = "8", LocKey = "Nav_Tools", Header = "SECTION 8", Subtitle = "Outils réseau et système" },
        new() { Id = "uninst", Number = "9", LocKey = "Nav_Uninst", Header = "SECTION 9", Subtitle = "Désinstallation complète • zéro trace" },
        new() { Id = "net", Number = "10", LocKey = "Nav_Net", Header = "SECTION 10", Subtitle = "Package réseau dédié • latence & stabilité" },
        new() { Id = "profils", Number = "11", LocKey = "Nav_Prof", Header = "SECTION 11", Subtitle = "Profils 1-clic par jeu" },
    };

    public ObservableCollection<ToolItem> Tools { get; } = new(
        TweakLibrary.All.Select(d => new ToolItem
        {
            ActionId = d.ActionId,
            Title = d.Title,
            Badge = d.Badge,
            Description = d.Title,
            SectionIds = d.Sections.ToList(),
            Risk = TweakLibrary.RiskOf(d.ActionId),
            GainText = TweakLibrary.GainTextOf(d.ActionId),
            GainSort = TweakLibrary.GainSortOf(d.ActionId)
        }));

    private int _maxRisk = 1;
    public int MaxRisk
    {
        get => _maxRisk;
        set { _maxRisk = Math.Clamp(value, 0, 2); PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MaxRisk))); RefreshSubtitle(); }
    }

    public void LoadRisk()
    {
        if (int.TryParse(SettingsService.Get("risk", "1"), out int r))
        {
            // Affectation directe + notification (sans RefreshSubtitle : SelectedSection pas encore assigné dans le ctor)
            _maxRisk = Math.Clamp(r, 0, 2);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(MaxRisk)));
        }
    }

    public void SetRisk(int level)
    {
        MaxRisk = level;
        SettingsService.Set("risk", level.ToString());
    }

    private SectionItem _selectedSection = null!;
    public SectionItem SelectedSection
    {
        get => _selectedSection;
        set
        {
            _selectedSection = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedSection)));
            RefreshSubtitle();
        }
    }

    private string _toolsSubtitle = "";
    public string ToolsSubtitle
    {
        get => _toolsSubtitle;
        set { _toolsSubtitle = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ToolsSubtitle))); }
    }

    public MainViewModel()
    {
        LoadRisk();
        _selectedSection = Sections[2]; // FPS & Performance par défaut
        _selectedSection.IsSelected = true;
        ApplyLanguage();
        foreach (var t in Tools) FpsGainService.ApplyMeasured(t); // gains réels mesurés (si dispo)
        RefreshSubtitle();
        Loc.Instance.LanguageChanged += () => { ApplyLanguage(); RefreshSubtitle(); };
    }

    public void ApplyLanguage()
    {
        foreach (var s in Sections) s.Title = Loc.Instance.Get(s.LocKey);
    }

    public void SelectSection(SectionItem section)
    {
        foreach (var s in Sections) s.IsSelected = s == section;
        SelectedSection = section;
    }

    public void RefreshSubtitle()
    {
        if (SelectedSection.Id == "home")
            ToolsSubtitle = Loc.Instance.Get("Sub_Home");
        else if (SelectedSection.Id == "uninst")
            ToolsSubtitle = Loc.Instance.Get("Sub_Uninst", Apps.Count);
        else
            ToolsSubtitle = Loc.Instance.Get("Sub_Tools", Tools.Count(t => t.SectionIds.Contains(SelectedSection.Id) && t.Risk <= MaxRisk));
    }

    public ToolItem? FindTool(string actionId) =>
        Tools.FirstOrDefault(t => t.ActionId == actionId);

    // ---------- Uninstaller ----------
    public ObservableCollection<InstalledApp> Apps { get; } = new();

    // File de désinstallation multiple (clés "Hive|View|RegistryKey")
    public HashSet<string> QueueKeys { get; } = new(StringComparer.OrdinalIgnoreCase);
    public int QueueCount => QueueKeys.Count;

    public static string QueueKeyOf(InstalledApp a) => $"{a.Hive}|{a.View}|{a.RegistryKey}";

    public void ToggleQueue(InstalledApp a)
    {
        string k = QueueKeyOf(a);
        if (!QueueKeys.Add(k)) QueueKeys.Remove(k);
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QueueCount)));
    }

    public bool IsQueued(InstalledApp a) => QueueKeys.Contains(QueueKeyOf(a));

    public void ClearQueue()
    {
        if (QueueKeys.Count == 0) return;
        QueueKeys.Clear();
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QueueCount)));
    }

    /// <summary>Retire de la file les apps qui ne sont plus listées (après refresh).</summary>
    public void PruneQueue()
    {
        var existing = new HashSet<string>(Apps.Select(QueueKeyOf), StringComparer.OrdinalIgnoreCase);
        if (QueueKeys.RemoveWhere(k => !existing.Contains(k)) > 0)
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(QueueCount)));
    }

    private InstalledApp? _selectedApp;
    public InstalledApp? SelectedApp
    {
        get => _selectedApp;
        set { _selectedApp = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(SelectedApp))); }
    }

    public ObservableCollection<LeftoverItem> Leftovers { get; } = new();
    public bool AppsLoaded { get; set; }

    private string _appSearch = "";
    public string AppSearch
    {
        get => _appSearch;
        set { _appSearch = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AppSearch))); }
    }

    private string _search = "";
    public string Search
    {
        get => _search;
        set { _search = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Search))); }
    }

    private int _executed;
    public int Executed
    {
        get => _executed;
        set { _executed = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Executed))); }
    }

    private double _cpu;
    public double Cpu
    {
        get => _cpu;
        set { _cpu = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Cpu))); }
    }

    private double _ram;
    public double Ram
    {
        get => _ram;
        set { _ram = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Ram))); }
    }

    private string _status = "Prêt";
    public string Status
    {
        get => _status;
        set { _status = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status))); }
    }

    private string _gpu = "—";
    public string Gpu
    {
        get => _gpu;
        set { _gpu = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Gpu))); }
    }
}
