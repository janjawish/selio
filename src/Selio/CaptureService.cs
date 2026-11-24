using System.ComponentModel;
using System.Drawing.Imaging;
using System.Text;
using System.Runtime.InteropServices;

namespace Selio;

internal static class CaptureService
{
    public static Bitmap Capture(Rectangle monitor)
    {
        // Never switch desktops or attempt to acquire a secure desktop.
        nint desktop = NativeMethods.OpenInputDesktop(0, false, 1);
        if (desktop == 0) throw new InvalidOperationException("Le bureau Windows est verrouillé ou protégé.");
        try
        {
            var name = new StringBuilder(256);
            if (!NativeMethods.GetUserObjectInformation(desktop, 2, name, 512, out _) ||
                !string.Equals(name.ToString(), "Default", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("La capture d'un bureau sécurisé n'est pas autorisée.");
        }
        finally { NativeMethods.CloseDesktop(desktop); }

        var bitmap = new Bitmap(monitor.Width, monitor.Height, PixelFormat.Format32bppArgb);
        try
        {
            using var graphics = Graphics.FromImage(bitmap);
            graphics.CopyFromScreen(monitor.Location, Point.Empty, monitor.Size, CopyPixelOperation.SourceCopy);
            return bitmap;
        }
        catch (Exception ex) when (ex is Win32Exception or ExternalException or ArgumentException)
        {
            bitmap.Dispose();
            throw new InvalidOperationException("Impossible de capturer cet écran. Vérifiez que le bureau est accessible.", ex);
        }
    }

    public static Bitmap Crop(Bitmap captured, Rectangle rectangle)
    {
        rectangle.Intersect(new Rectangle(Point.Empty, captured.Size));
        if (rectangle.Width < 4 || rectangle.Height < 4)
            throw new ArgumentException("La zone est trop petite. Tracez un rectangle d'au moins 4 × 4 pixels.");
        return captured.Clone(rectangle, PixelFormat.Format32bppArgb);
    }
}
