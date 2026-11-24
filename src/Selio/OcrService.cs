using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Selio.Core;
using Windows.Globalization;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace Selio;

internal sealed record OcrLanguage(string Tag, string Name)
{
    public override string ToString() => Name;
}

internal static class OcrService
{
    public static IReadOnlyList<OcrLanguage> Languages() => OcrEngine.AvailableRecognizerLanguages
        .Select(l => new OcrLanguage(l.LanguageTag, l.NativeName)).OrderBy(l => l.Name).ToArray();

    public static Task<OcrDocument> RecognizeAsync(Bitmap image, string? preferred, CancellationToken cancellation) =>
        Task.Run(async () =>
        {
            cancellation.ThrowIfCancellationRequested();
            OcrEngine? engine = preferred == null
                ? OcrEngine.TryCreateFromUserProfileLanguages()
                : OcrEngine.TryCreateFromLanguage(new Language(preferred));
            if (engine == null && preferred == null)
            {
                var fallback = OcrEngine.AvailableRecognizerLanguages.FirstOrDefault(l => l.LanguageTag.StartsWith("fr"))
                    ?? OcrEngine.AvailableRecognizerLanguages.FirstOrDefault(l => l.LanguageTag.StartsWith("en"))
                    ?? OcrEngine.AvailableRecognizerLanguages.FirstOrDefault();
                if (fallback != null) engine = OcrEngine.TryCreateFromLanguage(fallback);
            }
            if (engine == null)
                throw new InvalidOperationException("Module OCR Windows absent pour cette langue. Installez la reconnaissance optique dans Paramètres Windows > Heure et langue > Langue et région > Options linguistiques, puis réessayez.");

            var scale = ImageScale.Fit(image.Size, checked((int)OcrEngine.MaxImageDimension));
            Bitmap? resized = null;
            try
            {
                Bitmap source = image;
                if (scale.Target != scale.Source)
                {
                    resized = new Bitmap(scale.Target.Width, scale.Target.Height, PixelFormat.Format32bppArgb);
                    using var graphics = Graphics.FromImage(resized);
                    graphics.InterpolationMode = InterpolationMode.HighQualityBicubic;
                    graphics.DrawImage(image, new Rectangle(Point.Empty, resized.Size));
                    source = resized;
                }

                using var software = ToSoftwareBitmap(source);
                cancellation.ThrowIfCancellationRequested();
                var result = await engine.RecognizeAsync(software).AsTask(cancellation).ConfigureAwait(false);
                cancellation.ThrowIfCancellationRequested();
                var words = new List<RecognizedWord>();
                for (int line = 0; line < result.Lines.Count; line++)
                {
                    var items = result.Lines[line].Words;
                    for (int index = 0; index < items.Count; index++)
                    {
                        var r = items[index].BoundingRect;
                        words.Add(new(items[index].Text,
                            scale.ToSource(new RectangleF((float)r.X, (float)r.Y, (float)r.Width, (float)r.Height)), line, index));
                    }
                }
                return new OcrDocument(words, result.TextAngle);
            }
            finally { resized?.Dispose(); }
        }, cancellation);

    private static SoftwareBitmap ToSoftwareBitmap(Bitmap image)
    {
        var data = image.LockBits(new Rectangle(Point.Empty, image.Size), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            int rowBytes = checked(image.Width * 4);
            byte[] pixels = new byte[checked(rowBytes * image.Height)];
            for (int row = 0; row < image.Height; row++)
                Marshal.Copy(data.Scan0 + row * data.Stride, pixels, row * rowBytes, rowBytes);
            return SoftwareBitmap.CreateCopyFromBuffer(pixels.AsBuffer(), BitmapPixelFormat.Bgra8,
                image.Width, image.Height, BitmapAlphaMode.Ignore);
        }
        finally { image.UnlockBits(data); }
    }
}
