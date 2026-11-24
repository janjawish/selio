using System.Drawing;

namespace Selio.Core;

/// <summary>All capture/selection coordinates are physical pixels, relative to one monitor.</summary>
public static class Geometry
{
    public static Rectangle DragRectangle(Point start, Point end, Size bounds)
    {
        int x1 = Math.Clamp(Math.Min(start.X, end.X), 0, bounds.Width);
        int y1 = Math.Clamp(Math.Min(start.Y, end.Y), 0, bounds.Height);
        int x2 = Math.Clamp(Math.Max(start.X, end.X), 0, bounds.Width);
        int y2 = Math.Clamp(Math.Max(start.Y, end.Y), 0, bounds.Height);
        return Rectangle.FromLTRB(x1, y1, x2, y2);
    }

    public static Point ScreenToCapture(Point point, Rectangle monitor) =>
        new(point.X - monitor.X, point.Y - monitor.Y);

    public static Point CaptureToScreen(Point point, Rectangle monitor) =>
        new(point.X + monitor.X, point.Y + monitor.Y);

    public static int DipToPixel(double dip, int dpi) =>
        (int)Math.Round(dip * dpi / 96d, MidpointRounding.AwayFromZero);

    public static double PixelToDip(int pixel, int dpi) => pixel * 96d / dpi;

    public static Rectangle PlacePopup(Rectangle workArea, Size desiredSize, int dpi)
    {
        int width = Math.Min(desiredSize.Width, workArea.Width);
        int height = Math.Min(desiredSize.Height, workArea.Height);
        int top = Math.Min(DipToPixel(64, dpi), Math.Max(0, workArea.Height - height));
        return new(workArea.X + (workArea.Width - width) / 2, workArea.Y + top, width, height);
    }
}

public readonly record struct ImageScale(Size Source, Size Target)
{
    public static ImageScale Fit(Size source, int maximum)
    {
        if (source.Width <= 0 || source.Height <= 0 || maximum <= 0)
            throw new ArgumentOutOfRangeException(nameof(source));
        double scale = Math.Min(1, maximum / (double)Math.Max(source.Width, source.Height));
        return new(source, new(Math.Max(1, (int)Math.Floor(source.Width * scale)),
            Math.Max(1, (int)Math.Floor(source.Height * scale))));
    }

    // Use the two actual rounded dimensions, not the theoretical uniform factor.
    public RectangleF ToSource(RectangleF bounds) => new(
        bounds.X * Source.Width / Target.Width,
        bounds.Y * Source.Height / Target.Height,
        bounds.Width * Source.Width / Target.Width,
        bounds.Height * Source.Height / Target.Height);
}
