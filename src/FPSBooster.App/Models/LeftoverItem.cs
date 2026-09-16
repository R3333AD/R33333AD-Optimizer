using System.ComponentModel;
using static FPSBooster.App.Services.UninstallerService;

namespace FPSBooster.App.Models;

public class LeftoverItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public Leftover Source { get; set; } = null!;
    public string Kind => Source.Kind;
    public string Display => Source.Display;

    private bool _isSelected = true;
    public bool IsSelected
    {
        get => _isSelected;
        set { _isSelected = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected))); }
    }
}
