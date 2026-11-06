using System.Windows;
using System.Windows.Controls;

namespace StatsInfo.Views;

/// <summary>Measures complete metric tiles and wraps them in their saved order.</summary>
public sealed class ResponsiveMetricPanel : Panel
{
    public Size[] TileSizes { get; private set; } = [];

    protected override Size MeasureOverride(Size availableSize)
    {
        foreach (UIElement child in InternalChildren)
            child.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        TileSizes = InternalChildren.Cast<UIElement>().Select(c => c.DesiredSize).ToArray();
        return Pack(TileSizes, availableSize.Width);
    }

    public static Size Pack(IReadOnlyList<Size> tiles, double width)
    {
        double x = 0, y = 0, rowHeight = 0, usedWidth = 0;
        foreach (var tile in tiles)
        {
            if (x > 0 && x + tile.Width > width + 0.01)
            {
                usedWidth = Math.Max(usedWidth, x);
                y += rowHeight;
                x = rowHeight = 0;
            }
            x += tile.Width;
            rowHeight = Math.Max(rowHeight, tile.Height);
        }
        return new Size(Math.Max(usedWidth, x), y + rowHeight);
    }

    protected override Size ArrangeOverride(Size finalSize)
    {
        double x = 0, y = 0, rowHeight = 0;
        foreach (UIElement child in InternalChildren)
        {
            var tile = child.DesiredSize;
            if (x > 0 && x + tile.Width > finalSize.Width + 0.01)
            {
                y += rowHeight;
                x = rowHeight = 0;
            }
            child.Arrange(new Rect(x, y, Math.Min(tile.Width, finalSize.Width), tile.Height));
            x += tile.Width;
            rowHeight = Math.Max(rowHeight, tile.Height);
        }
        return finalSize;
    }
}
