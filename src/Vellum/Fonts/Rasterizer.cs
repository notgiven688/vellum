namespace Vellum;

/// <summary>
/// Scanline rasterizer for quadratic Bezier outlines.
/// Mirrors the stb_truetype edge-list rasterizer.
/// </summary>
internal static class Rasterizer
{
    private readonly struct Edge
    {
        public readonly float X0, Y0, X1, Y1;
        public readonly int Direction; // +1 or -1 (winding)
        public Edge(float x0, float y0, float x1, float y1, int dir)
        { X0 = x0; Y0 = y0; X1 = x1; Y1 = y1; Direction = dir; }
    }

    [ThreadStatic] private static List<Edge>? _edgeScratch;
    [ThreadStatic] private static List<(float x, int dir)>? _intersectionScratch;
    [ThreadStatic] private static float[]? _rowScratch;

    public static byte[] Rasterize(GlyphOutline outline, int width, int height, float offsetX, float offsetY)
    {
        var edges = _edgeScratch ??= new List<Edge>(512);
        edges.Clear();
        BuildEdges(edges, outline, offsetX, offsetY, 1f);
        var coverage = new byte[width * height];
        ScanlineFill(edges, coverage, width, height);
        return coverage;
    }

    /// <summary>
    /// Rasterizes at 3× horizontal resolution and splits into RGBA where
    /// R = left-subpixel coverage, G = centre, B = right, A = max(R,G,B).
    /// </summary>
    public static byte[] RasterizeLcd(GlyphOutline outline, int width, int height,
                                      float offsetX, float offsetY)
    {
        int wideW = width * 3;
        var edges = _edgeScratch ??= new List<Edge>(512);
        edges.Clear();
        BuildEdges(edges, outline, offsetX, offsetY, 3f);
        var wide  = new byte[wideW * height];
        ScanlineFill(edges, wide, wideW, height);

        // Optional box-filter pass to soften fringing
        var filtered = new byte[wideW * height];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < wideW; x++)
        {
            int i = y * wideW + x;
            int l = x > 0      ? wide[i - 1] : wide[i];
            int r = x < wideW-1 ? wide[i + 1] : wide[i];
            filtered[i] = (byte)((l + wide[i] * 2 + r) >> 2);
        }

        var rgba = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            byte rv = filtered[y * wideW + 3 * x + 0];
            byte gv = filtered[y * wideW + 3 * x + 1];
            byte bv = filtered[y * wideW + 3 * x + 2];
            byte av = Math.Max(rv, Math.Max(gv, bv));
            int dst = (y * width + x) * 4;
            rgba[dst + 0] = rv;
            rgba[dst + 1] = gv;
            rgba[dst + 2] = bv;
            rgba[dst + 3] = av;
        }
        return rgba;
    }

    private static void BuildEdges(List<Edge> edges, GlyphOutline outline, float ox, float oy, float hScale)
    {
        foreach (var contour in outline.Contours)
        {
            int n = contour.Length;
            if (n < 2) continue;

            int i = 0;
            while (i < n)
            {
                var p0 = contour[i];
                var p1 = contour[(i + 1) % n];

                // X is scaled by hScale after the bitmap offset is applied
                if (p0.OnCurve && p1.OnCurve)
                {
                    AddEdge(edges,
                        (p0.X + ox) * hScale, -p0.Y + oy,
                        (p1.X + ox) * hScale, -p1.Y + oy);
                    i++;
                }
                else if (p0.OnCurve && !p1.OnCurve)
                {
                    var p2 = contour[(i + 2) % n];
                    FlattenQuadratic(edges,
                        (p0.X + ox) * hScale, -p0.Y + oy,
                        (p1.X + ox) * hScale, -p1.Y + oy,
                        (p2.X + ox) * hScale, -p2.Y + oy);
                    i += 2;
                }
                else { i++; }
            }
        }
    }

    private static void FlattenQuadratic(List<Edge> edges, float x0, float y0, float cx, float cy, float x1, float y1)
    {
        float dx = x0 - 2 * cx + x1;
        float dy = y0 - 2 * cy + y1;
        if (dx * dx + dy * dy < 0.25f) { AddEdge(edges, x0, y0, x1, y1); return; }
        float mx = (x0 + 2 * cx + x1) * 0.25f;
        float my = (y0 + 2 * cy + y1) * 0.25f;
        float lx = (x0 + cx) * 0.5f, ly = (y0 + cy) * 0.5f;
        float rx = (cx + x1) * 0.5f, ry = (cy + y1) * 0.5f;
        FlattenQuadratic(edges, x0, y0, lx, ly, mx, my);
        FlattenQuadratic(edges, mx, my, rx, ry, x1, y1);
    }

    private static void AddEdge(List<Edge> edges, float x0, float y0, float x1, float y1)
    {
        if (y0 == y1) return;
        if (y0 < y1) edges.Add(new Edge(x0, y0, x1, y1,  1));
        else         edges.Add(new Edge(x1, y1, x0, y0, -1));
    }

    private static int CompareIntersectionX((float x, int dir) a, (float x, int dir) b)
        => a.x.CompareTo(b.x);

    private static void ScanlineFill(List<Edge> edges, byte[] coverage, int width, int height)
    {
        // 4×4 grid supersampling — each pixel gets 16 sub-samples, covering both
        // X and Y directions so diagonal and horizontal edges are properly anti-aliased.
        const int SY = 4, SX = 4;
        const float InvSamples = 1f / (SX * SY);

        var row = _rowScratch;
        if (row == null || row.Length < width)
            _rowScratch = row = new float[Math.Max(width, 64)];

        var xs = _intersectionScratch ??= new List<(float x, int dir)>(16);

        for (int y = 0; y < height; y++)
        {
            Array.Clear(row, 0, width);

            for (int sy = 0; sy < SY; sy++)
            {
                float fy = y + (sy + 0.5f) / SY;

                // Compute sorted X intersections for this sub-scanline once
                xs.Clear();
                foreach (var e in edges)
                {
                    if (fy < e.Y0 || fy >= e.Y1) continue;
                    float t  = (fy - e.Y0) / (e.Y1 - e.Y0);
                    float ix = e.X0 + t * (e.X1 - e.X0);
                    xs.Add((ix, e.Direction));
                }
                xs.Sort(CompareIntersectionX);

                // For each pixel, test SX horizontal sub-samples
                for (int x = 0; x < width; x++)
                {
                    for (int sx = 0; sx < SX; sx++)
                    {
                        float fx = x + (sx + 0.5f) / SX;
                        int wind = 0;
                        foreach (var (ix, dir) in xs)
                        {
                            if (ix > fx) break;
                            wind += dir;
                        }
                        if (wind != 0) row[x] += InvSamples;
                    }
                }
            }

            int rowBase = y * width;
            for (int x = 0; x < width; x++)
                coverage[rowBase + x] = (byte)(row[x] * 255f + 0.5f);
        }
    }
}
