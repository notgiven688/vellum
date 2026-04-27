namespace Vellum;

/// <summary>
/// Parses TrueType font binary data and locates table offsets.
/// Mirrors stbtt_fontinfo from stb_truetype.h.
/// </summary>
internal sealed class FontParser
{
    private readonly byte[] _data;
    internal readonly int IndexMap;     // cmap encoding offset for the selected platform
    internal readonly int IndexLocFormat; // 0 = short offsets, 1 = long offsets (from head table)

    // Table offsets (-1 = not present)
    internal readonly int CmapOffset;
    internal readonly int GlyfOffset;
    internal readonly int HeadOffset;
    internal readonly int HheaOffset;
    internal readonly int HmtxOffset;
    internal readonly int KernOffset;
    internal readonly int LocaOffset;
    internal readonly int MaxpOffset;
    internal readonly int NumGlyphs;
    internal readonly int NumHMetrics;

    internal FontParser(byte[] data)
    {
        _data = data;

        // Offset table: sfVersion(4) numTables(2) searchRange(2) entrySelector(2) rangeShift(2)
        int numTables = ReadU16(4);

        CmapOffset = -1; GlyfOffset = -1; HeadOffset = -1; HheaOffset = -1;
        HmtxOffset = -1; KernOffset = -1; LocaOffset = -1; MaxpOffset = -1;

        for (int i = 0; i < numTables; i++)
        {
            int tableOffset = 12 + i * 16;
            string tag = ReadTag(tableOffset);
            int offset = ReadI32(tableOffset + 8);
            switch (tag)
            {
                case "cmap": CmapOffset = offset; break;
                case "glyf": GlyfOffset = offset; break;
                case "head": HeadOffset = offset; break;
                case "hhea": HheaOffset = offset; break;
                case "hmtx": HmtxOffset = offset; break;
                case "kern": KernOffset = offset; break;
                case "loca": LocaOffset = offset; break;
                case "maxp": MaxpOffset = offset; break;
            }
        }

        NumGlyphs = ReadU16(MaxpOffset + 4);
        IndexLocFormat = ReadI16(HeadOffset + 50);
        NumHMetrics = ReadU16(HheaOffset + 34);

        // Find a usable cmap subtable: prefer platform 3 (Windows) encoding 1 (Unicode BMP)
        // then platform 0 (Unicode), then platform 3 encoding 0
        int cmapCount = ReadU16(CmapOffset + 2);
        int found = -1;
        int foundFormat = -1;

        for (int i = 0; i < cmapCount; i++)
        {
            int rec = CmapOffset + 4 + i * 8;
            int platformId = ReadU16(rec);
            int encodingId = ReadU16(rec + 2);
            int subtableOffset = CmapOffset + ReadI32(rec + 4);
            int format = ReadU16(subtableOffset);

            if (platformId == 3 && encodingId == 1 && (format == 4 || format == 12))
            {
                found = subtableOffset; foundFormat = format; break;
            }
            if (platformId == 0 && (format == 4 || format == 12) && found == -1)
            {
                found = subtableOffset; foundFormat = format;
            }
        }

        IndexMap = found;
        _ = foundFormat; // stored implicitly; format is re-read at lookup time
    }

    // -------------------------------------------------------------------------
    // Cmap: Unicode codepoint → glyph index
    // -------------------------------------------------------------------------

    internal int FindGlyphIndex(int codepoint)
    {
        if (IndexMap < 0) return 0;
        int format = ReadU16(IndexMap);

        if (format == 4)
            return FindGlyphFormat4(codepoint);
        if (format == 12)
            return FindGlyphFormat12(codepoint);
        return 0;
    }

    private int FindGlyphFormat4(int codepoint)
    {
        if (codepoint > 0xffff) return 0;
        int segCount = ReadU16(IndexMap + 6) >> 1;
        int endBase = IndexMap + 14;
        int startBase = endBase + 2 + segCount * 2;
        int deltaBase = startBase + segCount * 2;
        int rangeBase = deltaBase + segCount * 2;

        // Search segments directly by endCode. This is simpler and avoids the
        // fragile searchRange/entrySelector arithmetic from the packed table.
        int lo = 0;
        int hi = segCount - 1;
        while (lo <= hi)
        {
            int mid = lo + ((hi - lo) >> 1);
            int segmentEnd = ReadU16(endBase + mid * 2);
            if (codepoint > segmentEnd) lo = mid + 1;
            else hi = mid - 1;
        }

        int item = lo;
        if (item >= segCount) return 0;
        int start = ReadU16(startBase + item * 2);
        int end = ReadU16(endBase + item * 2);
        if (codepoint < start || codepoint > end) return 0;

        int rangeOffset = ReadU16(rangeBase + item * 2);
        if (rangeOffset == 0)
        {
            return (codepoint + ReadI16(deltaBase + item * 2)) & 0xffff;
        }
        int glyphIndex = ReadU16(rangeBase + item * 2 + rangeOffset + (codepoint - start) * 2);
        if (glyphIndex == 0) return 0;
        return (glyphIndex + ReadI16(deltaBase + item * 2)) & 0xffff;
    }

    private int FindGlyphFormat12(int codepoint)
    {
        int nGroups = ReadI32(IndexMap + 12);
        int groupBase = IndexMap + 16;
        int lo = 0, hi = nGroups;
        while (lo < hi)
        {
            int mid = (lo + hi) >> 1;
            int startChar = ReadI32(groupBase + mid * 12);
            int endChar = ReadI32(groupBase + mid * 12 + 4);
            if (codepoint < startChar) hi = mid;
            else if (codepoint > endChar) lo = mid + 1;
            else return ReadI32(groupBase + mid * 12 + 8) + (codepoint - startChar);
        }
        return 0;
    }

    // -------------------------------------------------------------------------
    // Glyph data
    // -------------------------------------------------------------------------

    internal int GetGlyphOffset(int glyphIndex)
    {
        if (glyphIndex >= NumGlyphs) return -1;
        int g1, g2;
        if (IndexLocFormat == 0)
        {
            g1 = GlyfOffset + ReadU16(LocaOffset + glyphIndex * 2) * 2;
            g2 = GlyfOffset + ReadU16(LocaOffset + glyphIndex * 2 + 2) * 2;
        }
        else
        {
            g1 = GlyfOffset + ReadI32(LocaOffset + glyphIndex * 4);
            g2 = GlyfOffset + ReadI32(LocaOffset + glyphIndex * 4 + 4);
        }
        return g1 == g2 ? -1 : g1;
    }

    internal GlyphMetrics GetGlyphMetrics(int glyphIndex)
    {
        int advanceWidth, lsb;
        if (glyphIndex < NumHMetrics)
        {
            advanceWidth = ReadU16(HmtxOffset + glyphIndex * 4);
            lsb = ReadI16(HmtxOffset + glyphIndex * 4 + 2);
        }
        else
        {
            advanceWidth = ReadU16(HmtxOffset + (NumHMetrics - 1) * 4);
            lsb = ReadI16(HmtxOffset + NumHMetrics * 4 + (glyphIndex - NumHMetrics) * 2);
        }

        int glyfOff = GetGlyphOffset(glyphIndex);
        if (glyfOff < 0)
            return new GlyphMetrics(advanceWidth, lsb, 0, 0, 0, 0);

        int x0 = ReadI16(glyfOff + 2);
        int y0 = ReadI16(glyfOff + 4);
        int x1 = ReadI16(glyfOff + 6);
        int y1 = ReadI16(glyfOff + 8);
        return new GlyphMetrics(advanceWidth, lsb, x0, y0, x1, y1);
    }

    internal FontVMetrics GetFontVMetrics()
    {
        int ascent = ReadI16(HheaOffset + 4);
        int descent = ReadI16(HheaOffset + 6);
        int lineGap = ReadI16(HheaOffset + 8);
        return new FontVMetrics(ascent, descent, lineGap);
    }

    internal int GetKernAdvance(int glyph1, int glyph2)
    {
        if (KernOffset < 0) return 0;
        int nTables = ReadU16(KernOffset + 2);
        int offset = KernOffset + 4;
        for (int k = 0; k < nTables; k++)
        {
            int coverage = ReadU16(offset + 4);
            int length = ReadU16(offset + 2);
            // Only horizontal kerning, format 0
            if ((coverage & 1) != 0 && (coverage >> 8) == 0)
            {
                int nPairs = ReadU16(offset + 6);
                int pairBase = offset + 14;
                // Binary search
                int lo = 0, hi = nPairs - 1;
                while (lo <= hi)
                {
                    int mid = (lo + hi) >> 1;
                    int g1 = ReadU16(pairBase + mid * 6);
                    int g2 = ReadU16(pairBase + mid * 6 + 2);
                    if (g1 < glyph1 || (g1 == glyph1 && g2 < glyph2)) lo = mid + 1;
                    else if (g1 > glyph1 || g2 > glyph2) hi = mid - 1;
                    else return ReadI16(pairBase + mid * 6 + 4);
                }
            }
            offset += length;
        }
        return 0;
    }

    // -------------------------------------------------------------------------
    // Raw reads (big-endian)
    // -------------------------------------------------------------------------

    internal byte ReadU8(int offset) => _data[offset];
    internal int ReadU16(int offset) => (_data[offset] << 8) | _data[offset + 1];
    internal int ReadI16(int offset) => (short)((_data[offset] << 8) | _data[offset + 1]);
    internal int ReadI32(int offset) =>
        (_data[offset] << 24) | (_data[offset + 1] << 16) | (_data[offset + 2] << 8) | _data[offset + 3];

    private string ReadTag(int offset) =>
        new string(new[] { (char)_data[offset], (char)_data[offset+1], (char)_data[offset+2], (char)_data[offset+3] });
}
