using System.ComponentModel;

namespace FPSBooster.App.Models;

public class SectionItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string Id { get; set; } = "";
    public string Number { get; set; } = ""; // "⌂" = Home (icône)
    public string LocKey { get; set; } = "";
    public string Header { get; set; } = ""; // "SECTION 2" / "OVERVIEW"
    public string Subtitle { get; set; } = "";

    private string _title = "";
    public string Title
    {
        get => _title;
        set { _title = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Title))); }
    }

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
    }
}
