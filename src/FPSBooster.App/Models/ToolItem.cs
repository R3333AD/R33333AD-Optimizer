using System.ComponentModel;
using System.Windows.Media;

namespace FPSBooster.App.Models;

public class ToolItem : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    public string ActionId { get; set; } = "";
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public string Badge { get; set; } = "REG"; // CMD ou REG
    public List<string> SectionIds { get; set; } = new() { "fps" };
    public int Risk { get; set; } = 1; // 0=Léger 1=Normal 2=Expert

    private string _gainText = "~0 %";
    public string GainText
    {
        get => _gainText;
        set { _gainText = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GainText))); RefreshGainColor(); }
    }

    private double _gainSort;
    public double GainSort // valeur numérique pour trier (mesuré réel si dispo, sinon estimation)
    {
        get => _gainSort;
        set { _gainSort = value; RefreshGainColor(); }
    }

    private SolidColorBrush _gainBrush = new(Color.FromRgb(0xC8, 0xFF, 0x2E));
    public SolidColorBrush GainBrush
    {
        get => _gainBrush;
        private set { _gainBrush = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(GainBrush))); }
    }

    /// <summary>Pastille couleur : vert vif si mesuré &gt; +3 %, gris si ~0, rouge si négatif,
    /// bleu clair si latence/ping, gris foncé si non mesurable, lime si estimation.</summary>
    public void RefreshGainColor()
    {
        try
        {
            if (_gainText.StartsWith("mesuré", StringComparison.OrdinalIgnoreCase))
            {
                GainBrush = _gainSort > 3 ? new SolidColorBrush(Color.FromRgb(0x00, 0xE6, 0x76))
                    : _gainSort < 0 ? new SolidColorBrush(Color.FromRgb(0xFF, 0x5C, 0x5C))
                    : new SolidColorBrush(Color.FromRgb(0x8E, 0x8E, 0x93));
            }
            else if (_gainText == "—")
                GainBrush = new SolidColorBrush(Color.FromRgb(0x52, 0x52, 0x5B));
            else if (_gainText.Contains("lag") || _gainText.Contains("ping"))
                GainBrush = new SolidColorBrush(Color.FromRgb(0x7D, 0xD3, 0xFC));
            else
                GainBrush = new SolidColorBrush(Color.FromRgb(0xC8, 0xFF, 0x2E));
        }
        catch { GainBrush = new SolidColorBrush(Colors.White); }
    }

    private bool _isApplied;
    public bool IsApplied
    {
        get => _isApplied;
        set { _isApplied = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsApplied))); }
    }

    private bool _isRunning;
    public bool IsRunning
    {
        get => _isRunning;
        set { _isRunning = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsRunning))); }
    }
}

public record TweakResult(bool Success, string Message);
