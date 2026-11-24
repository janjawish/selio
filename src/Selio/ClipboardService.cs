using System.Runtime.InteropServices;

namespace Selio;

internal static class ClipboardService
{
    public static Task SetTextAsync(string text, CancellationToken cancellation) =>
        RetryAsync(() => Clipboard.SetDataObject(text, true, 1, 0), cancellation);

    public static Task SetImageAsync(Bitmap image, CancellationToken cancellation) =>
        RetryAsync(() =>
        {
            var data = new DataObject();
            data.SetData(DataFormats.Bitmap, true, image);
            // copy:true flushes the data into the system clipboard before disposing the bitmap.
            Clipboard.SetDataObject(data, true, 1, 0);
        }, cancellation);

    private static async Task RetryAsync(Action write, CancellationToken cancellation)
    {
        for (int attempt = 0; ; attempt++)
        {
            cancellation.ThrowIfCancellationRequested();
            try { write(); return; }
            catch (ExternalException) when (attempt < 5)
            { await Task.Delay(60 * (attempt + 1), cancellation); }
            catch (ExternalException ex)
            { throw new InvalidOperationException("Le presse-papiers est occupé. Fermez l'application qui le bloque, puis réessayez.", ex); }
        }
    }
}
