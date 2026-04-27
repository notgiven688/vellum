namespace Vellum;

internal readonly struct GlyphPoint
{
    public readonly float X, Y;
    public readonly bool OnCurve;
    public GlyphPoint(float x, float y, bool onCurve) { X = x; Y = y; OnCurve = onCurve; }
}

internal sealed class GlyphOutline
{
    // Each contour is a list of points; the contour is implicitly closed.
    public readonly List<GlyphPoint[]> Contours = new();

    public static GlyphOutline? Load(FontParser font, int glyphIndex, float scale)
    {
        int glyfOff = font.GetGlyphOffset(glyphIndex);
        if (glyfOff < 0) return null;

        int numContours = font.ReadI16(glyfOff);
        if (numContours > 0)
            return LoadSimple(font, glyfOff, numContours, scale);
        if (numContours == -1)
            return LoadComposite(font, glyfOff, scale);
        return null;
    }

    private static GlyphOutline LoadSimple(FontParser font, int glyfOff, int numContours, float scale)
    {
        var outline = new GlyphOutline();
        int offset = glyfOff + 10; // skip header (10 bytes)

        int[] endPtsOfContours = new int[numContours];
        for (int i = 0; i < numContours; i++)
        {
            endPtsOfContours[i] = font.ReadU16(offset);
            offset += 2;
        }
        int numPoints = endPtsOfContours[numContours - 1] + 1;

        int instructionLength = font.ReadU16(offset);
        offset += 2 + instructionLength; // skip instructions

        // Decode flags
        byte[] flags = new byte[numPoints];
        for (int i = 0; i < numPoints;)
        {
            byte flag = font.ReadU8(offset++);
            flags[i++] = flag;
            if ((flag & 8) != 0) // repeat flag
            {
                int repeat = font.ReadU8(offset++);
                for (int r = 0; r < repeat; r++)
                    flags[i++] = flag;
            }
        }

        // Decode x coordinates
        int[] xs = new int[numPoints];
        int cx = 0;
        for (int i = 0; i < numPoints; i++)
        {
            byte flag = flags[i];
            if ((flag & 2) != 0)
            {
                int dx = font.ReadU8(offset++);
                cx += (flag & 16) != 0 ? dx : -dx;
            }
            else if ((flag & 16) == 0)
            {
                cx += font.ReadI16(offset); offset += 2;
            }
            xs[i] = cx;
        }

        // Decode y coordinates
        int[] ys = new int[numPoints];
        int cy = 0;
        for (int i = 0; i < numPoints; i++)
        {
            byte flag = flags[i];
            if ((flag & 4) != 0)
            {
                int dy = font.ReadU8(offset++);
                cy += (flag & 32) != 0 ? dy : -dy;
            }
            else if ((flag & 32) == 0)
            {
                cy += font.ReadI16(offset); offset += 2;
            }
            ys[i] = cy;
        }

        // Build contours, expanding implicit on-curve points between two off-curve points
        int start = 0;
        for (int c = 0; c < numContours; c++)
        {
            int end = endPtsOfContours[c];
            var points = new List<GlyphPoint>();
            ExpandContour(flags, xs, ys, start, end, scale, points);
            outline.Contours.Add(points.ToArray());
            start = end + 1;
        }

        return outline;
    }

    private static void ExpandContour(byte[] flags, int[] xs, int[] ys, int start, int end, float scale, List<GlyphPoint> points)
    {
        // TrueType quadratic splines: when two consecutive off-curve points appear,
        // there is an implicit on-curve point at their midpoint.
        int count = end - start + 1;
        for (int i = 0; i < count; i++)
        {
            int cur = start + i;
            int next = start + (i + 1) % count;
            bool curOn = (flags[cur] & 1) != 0;
            bool nextOn = (flags[next] & 1) != 0;

            points.Add(new GlyphPoint(xs[cur] * scale, ys[cur] * scale, curOn));

            if (!curOn && !nextOn)
            {
                // Insert implicit on-curve midpoint
                float mx = (xs[cur] + xs[next]) * 0.5f * scale;
                float my = (ys[cur] + ys[next]) * 0.5f * scale;
                points.Add(new GlyphPoint(mx, my, true));
            }
        }
    }

    private static GlyphOutline LoadComposite(FontParser font, int glyfOff, float scale)
    {
        var outline = new GlyphOutline();
        int offset = glyfOff + 10;
        int flags;
        do
        {
            flags = font.ReadU16(offset); offset += 2;
            int glyphIndex = font.ReadU16(offset); offset += 2;

            float dx = 0, dy = 0;
            if ((flags & 1) != 0) // ARG_1_AND_2_ARE_WORDS
            {
                dx = font.ReadI16(offset); offset += 2;
                dy = font.ReadI16(offset); offset += 2;
            }
            else
            {
                dx = (sbyte)font.ReadU8(offset++);
                dy = (sbyte)font.ReadU8(offset++);
            }

            // 2x2 transform matrix (we support only uniform scale + translate for now)
            float a = 1, b = 0, c = 0, d = 1;
            if ((flags & 8) != 0) // WE_HAVE_A_SCALE
            {
                a = d = font.ReadI16(offset) / 16384f; offset += 2;
            }
            else if ((flags & 64) != 0) // WE_HAVE_AN_X_AND_Y_SCALE
            {
                a = font.ReadI16(offset) / 16384f; offset += 2;
                d = font.ReadI16(offset) / 16384f; offset += 2;
            }
            else if ((flags & 128) != 0) // WE_HAVE_A_TWO_BY_TWO
            {
                a = font.ReadI16(offset) / 16384f; offset += 2;
                b = font.ReadI16(offset) / 16384f; offset += 2;
                c = font.ReadI16(offset) / 16384f; offset += 2;
                d = font.ReadI16(offset) / 16384f; offset += 2;
            }

            var component = Load(font, glyphIndex, scale);
            if (component != null)
            {
                foreach (var contour in component.Contours)
                {
                    var transformed = new GlyphPoint[contour.Length];
                    for (int i = 0; i < contour.Length; i++)
                    {
                        float px = contour[i].X;
                        float py = contour[i].Y;
                        transformed[i] = new GlyphPoint(
                            a * px + c * py + dx * scale,
                            b * px + d * py + dy * scale,
                            contour[i].OnCurve);
                    }
                    outline.Contours.Add(transformed);
                }
            }
        } while ((flags & 32) != 0); // MORE_COMPONENTS

        return outline;
    }
}
