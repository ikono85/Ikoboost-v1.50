using System.Windows.Media;

namespace IkoboostWpf.Models;

public sealed class HardwareSummaryCard
{
    public string Title { get; init; } = "";
    public string Value { get; init; } = "N/A";
    public string Unit { get; init; } = "";
    public string Subtitle { get; init; } = "";
    public string Glyph { get; init; } = "";
    public Brush AccentBrush { get; init; } = Brushes.Gray;
    public Brush BackgroundBrush { get; init; } = Brushes.Transparent;
}
