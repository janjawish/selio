using System.Drawing;
using System.Text;

namespace Selio.Core;

public sealed record RecognizedWord(string Text, RectangleF Bounds, int Line, int Position);

public sealed class OcrDocument
{
    public IReadOnlyList<RecognizedWord> Words { get; }
    public double? Angle { get; }

    public OcrDocument(IEnumerable<RecognizedWord> words, double? angle = null)
    {
        // Preserve the reading order from Windows OCR (including column decisions).
        Words = words.OrderBy(w => w.Line).ThenBy(w => w.Position).ToArray();
        Angle = angle;
    }

    public int HitTest(PointF point, float tolerance = 3)
    {
        for (int i = 0; i < Words.Count; i++)
            if (Words[i].Bounds.Contains(point)) return i;
        int nearest = -1;
        float distance = tolerance * tolerance;
        for (int i = 0; i < Words.Count; i++)
        {
            float d = DistanceSquared(point, Words[i].Bounds);
            if (d <= distance) { nearest = i; distance = d; }
        }
        return nearest;
    }

    public int NearestWord(PointF point)
    {
        if (Words.Count == 0) return -1;
        return Enumerable.Range(0, Words.Count).MinBy(i => DistanceSquared(point, Words[i].Bounds));
    }

    public IReadOnlyList<RecognizedWord> SelectRange(int anchor, int end)
    {
        if (anchor < 0 || end < 0 || anchor >= Words.Count || end >= Words.Count) return [];
        return Words.Skip(Math.Min(anchor, end)).Take(Math.Abs(end - anchor) + 1).ToArray();
    }

    public string Text => Reconstruct(Words);

    public static string Reconstruct(IEnumerable<RecognizedWord> words)
    {
        var output = new StringBuilder();
        int? previousLine = null;
        string previous = "";
        foreach (var word in words.OrderBy(w => w.Line).ThenBy(w => w.Position))
        {
            if (string.IsNullOrEmpty(word.Text)) continue;
            if (previousLine.HasValue)
            {
                if (word.Line != previousLine) output.AppendLine();
                else if (NeedsSpace(previous, word.Text)) output.Append(' ');
            }
            output.Append(word.Text);
            previous = word.Text;
            previousLine = word.Line;
        }
        return output.ToString();
    }

    private static bool NeedsSpace(string left, string right) =>
        !",.)]}".Contains(right[0]) && !"([{’'".Contains(left[^1]) && !"’'".Contains(right[0]);

    private static float DistanceSquared(PointF point, RectangleF bounds)
    {
        float dx = Math.Max(bounds.Left - point.X, Math.Max(0, point.X - bounds.Right));
        float dy = Math.Max(bounds.Top - point.Y, Math.Max(0, point.Y - bounds.Bottom));
        return dx * dx + dy * dy;
    }
}
