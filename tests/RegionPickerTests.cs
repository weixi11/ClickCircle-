using System;
using System.Collections.Generic;
using ClickCircle;
using PDFXEdit;

internal static class RegionPickerTests
{
    private static int checkedCases;

    private static tagRECT Rect(int left, int top, int right, int bottom)
    {
        return new tagRECT { left = left, top = top, right = right, bottom = bottom };
    }

    private static void AddText(List<RegionGlyph> glyphs, string text, uint index,
        uint line, int left, int top)
    {
        for (int i = 0; i < text.Length; i++)
        {
            glyphs.Add(new RegionGlyph
            {
                Index = index + (uint)i,
                LineIndex = line,
                Glyph = new TextPicker.Glyph
                {
                    Character = text[i],
                    PageBounds = new PXC_Rect
                    {
                        left = i * 6, right = i * 6 + 5, bottom = 0, top = 10
                    },
                    LineBounds = new PXC_RectF
                    {
                        left = i * 6, right = i * 6 + 5, bottom = 0, top = 10
                    }
                },
                DeviceBounds = Rect(left + i * 6, top, left + i * 6 + 5, top + 10)
            });
        }
    }

    private static void Expect(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        checkedCases++;
    }

    private static RegionPickResult ExpectWords(IList<RegionGlyph> glyphs, tagRECT rect,
        params string[] expected)
    {
        RegionPickResult result = TextRegionPicker.Select(glyphs, rect);
        Expect(result.Fragments.Length == expected.Length, "Fragment count differs.");
        for (int i = 0; i < expected.Length; i++)
            Expect(result.Fragments[i] == expected[i],
                "Fragment " + i + ": expected " + expected[i] + ", got " + result.Fragments[i]);
        return result;
    }

    private static List<RegionGlyph> Labels()
    {
        var glyphs = new List<RegionGlyph>();
        AddText(glyphs, "LI-282", 0, 0, 60, 80);
        AddText(glyphs, "LI-058", 30, 1, 100, 10);
        AddText(glyphs, "LI-058", 60, 2, 30, 40);
        return glyphs;
    }

    public static int Main()
    {
        try
        {
            List<RegionGlyph> glyphs = Labels();
            RegionPickResult all = ExpectWords(glyphs, Rect(0, 0, 200, 100),
                "LI-058", "LI-058", "LI-282");
            Expect(all.HighlightRects.Length == 18, "Every selected glyph is highlighted once.");
            ExpectWords(glyphs, Rect(200, 100, 0, 0), "LI-058", "LI-058", "LI-282");
            glyphs.Reverse();
            ExpectWords(glyphs, Rect(0, 0, 200, 100), "LI-058", "LI-058", "LI-282");

            var sameRow = new List<RegionGlyph>();
            AddText(sameRow, "RIGHT", 0, 0, 100, 11);
            AddText(sameRow, "LEFT", 50, 1, 10, 13);
            AddText(sameRow, "NEXT", 100, 2, 0, 30);
            ExpectWords(sameRow, Rect(-1, 0, 200, 100), "LEFT", "RIGHT", "NEXT");

            var text = new List<RegionGlyph>();
            AddText(text, "LI-058 LI-282", 0, 0, 0, 0);
            RegionPickResult spaced = ExpectWords(text, Rect(-1, -1, 100, 20), "LI-058", "LI-282");
            Expect(spaced.HighlightRects.Length == 12, "Space is not a highlight glyph.");
            ExpectWords(text, Rect(0, 0, 20, 10), "LI-");
            ExpectWords(text, Rect(3, 0, 14, 10), "I");
            ExpectWords(text, Rect(0, 0, 2, 10));
            ExpectWords(text, Rect(0, 10, 100, 20));
            ExpectWords(text, Rect(0, 0, 0, 10));
            ExpectWords(text, Rect(0, 0, 100, 0));
            ExpectWords(text, Rect(200, 200, 300, 300));

            var centreEdge = new List<RegionGlyph>();
            AddText(centreEdge, "X", 0, 0, 0, 0);
            RegionGlyph edgeGlyph = centreEdge[0];
            edgeGlyph.DeviceBounds = Rect(0, 0, 6, 10);
            centreEdge[0] = edgeGlyph;
            ExpectWords(centreEdge, Rect(3, 5, 4, 6), "X");
            edgeGlyph.DeviceBounds = Rect(6, 10, 0, 0);
            centreEdge[0] = edgeGlyph;
            ExpectWords(centreEdge, Rect(3, 5, 4, 6), "X");
            edgeGlyph.DeviceBounds = Rect(0, 0, 0, 10);
            centreEdge[0] = edgeGlyph;
            ExpectWords(centreEdge, Rect(-1, -1, 10, 20));

            var gaps = new List<RegionGlyph>();
            AddText(gaps, "AABB", 0, 0, 0, 0);
            for (int i = 2; i < gaps.Count; i++)
            {
                RegionGlyph glyph = gaps[i];
                glyph.Glyph.LineBounds.left += 20;
                glyph.Glyph.LineBounds.right += 20;
                glyph.DeviceBounds.left += 20;
                glyph.DeviceBounds.right += 20;
                gaps[i] = glyph;
            }
            ExpectWords(gaps, Rect(-1, -1, 100, 20), "AA", "BB");

            var skipped = new List<RegionGlyph>();
            AddText(skipped, "ABCD", 0, 0, 0, 0);
            RegionGlyph outOfBox = skipped[1];
            outOfBox.DeviceBounds = Rect(100, 100, 106, 110);
            skipped[1] = outOfBox;
            ExpectWords(skipped, Rect(-1, -1, 30, 20), "A", "CD");

            var sourceLines = new List<RegionGlyph>();
            AddText(sourceLines, "ONE", 0, 0, 0, 0);
            AddText(sourceLines, "TWO", 3, 1, 18, 0);
            ExpectWords(sourceLines, Rect(-1, -1, 100, 20), "ONE", "TWO");

            var baseline = new List<RegionGlyph>();
            AddText(baseline, "AB", 0, 0, 0, 0);
            RegionGlyph shifted = baseline[1];
            shifted.Glyph.LineBounds.top += 20;
            shifted.Glyph.LineBounds.bottom += 20;
            baseline[1] = shifted;
            ExpectWords(baseline, Rect(-1, -1, 100, 20), "A", "B");

            var unicode = new List<RegionGlyph>();
            AddText(unicode, "\u5e94\u6025\u7167\u660e", 0, 0, 0, 0);
            AddText(unicode, "LI-156x8rows", 50, 1, 0, 30);
            ExpectWords(unicode, Rect(-1, -1, 100, 100),
                "\u5e94\u6025\u7167\u660e", "LI-156x8rows");

            var controls = new List<RegionGlyph>();
            AddText(controls, "A\tB\ufffdC", 0, 0, 0, 0);
            ExpectWords(controls, Rect(-1, -1, 100, 20), "A", "B", "C");
            ExpectWords(new List<RegionGlyph>(), Rect(0, 0, 100, 100));

            string[][] rotationOrder = {
                new[] { "A", "B", "C" },
                new[] { "B", "A", "C" },
                new[] { "C", "B", "A" },
                new[] { "C", "A", "B" }
            };
            for (int rotation = 0; rotation < 4; rotation++)
            {
                var rotated = new List<RegionGlyph>();
                AddText(rotated, "A", 60, 2, 10, 10);
                AddText(rotated, "B", 30, 1, 10, 40);
                AddText(rotated, "C", 0, 0, 70, 40);
                for (int index = 0; index < rotated.Count; index++)
                {
                    RegionGlyph changed = rotated[index];
                    tagRECT rect = changed.DeviceBounds;
                    if (rotation == 1)
                        changed.DeviceBounds = Rect(-rect.bottom, rect.left, -rect.top, rect.right);
                    else if (rotation == 2)
                        changed.DeviceBounds = Rect(-rect.right, -rect.bottom, -rect.left, -rect.top);
                    else if (rotation == 3)
                        changed.DeviceBounds = Rect(rect.top, -rect.right, rect.bottom, -rect.left);
                    rotated[index] = changed;
                }
                ExpectWords(rotated, Rect(-100, -100, 200, 100), rotationOrder[rotation]);
            }

            Console.WriteLine("RegionPicker: " + checkedCases + " checks passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
