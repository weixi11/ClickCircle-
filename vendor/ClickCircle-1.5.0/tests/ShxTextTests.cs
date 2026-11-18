using System;
using System.Collections.Generic;
using ClickCircle;
using PDFXEdit;

internal static class ShxTextTests
{
    private static int checks;

    private static void Expect(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        checks++;
    }

    private static PXC_Rect Bounds(double left, double bottom, double right, double top)
    {
        return new PXC_Rect { left = left, bottom = bottom, right = right, top = top };
    }

    private static tagRECT Rect(int left, int top, int right, int bottom)
    {
        return new tagRECT { left = left, top = top, right = right, bottom = bottom };
    }

    private static ShxTextLabel Label(string text, PXC_Rect bounds)
    {
        ShxTextLabel label;
        Expect(ShxTextReader.TryCreate("AutoCAD SHX Text", text, bounds, out label),
            "A valid SHX original must be accepted.");
        return label;
    }

    private static RegionGlyph Region(string text, uint index, tagRECT bounds)
    {
        return new RegionGlyph { LabelText = text, Index = index, DeviceBounds = bounds };
    }

    private static void Native(List<RegionGlyph> glyphs, string text, int left, int top)
    {
        for (int i = 0; i < text.Length; i++)
            glyphs.Add(new RegionGlyph
            {
                Index = (uint)i,
                Glyph = new TextPicker.Glyph
                {
                    Character = text[i],
                    LineBounds = new PXC_RectF
                    {
                        left = i * 6, right = i * 6 + 6, top = 10, bottom = 0
                    }
                },
                DeviceBounds = Rect(left + i * 6, top, left + i * 6 + 6, top + 10)
            });
    }

    private static RegionPickResult Words(List<RegionGlyph> glyphs, tagRECT rect,
        params string[] words)
    {
        RegionPickResult result = TextRegionPicker.Select(glyphs, rect);
        Expect(result.Fragments.Length == words.Length, "Unexpected fragment count: " +
            String.Join("/", result.Fragments));
        for (int i = 0; i < words.Length; i++)
            Expect(result.Fragments[i] == words[i], "Unexpected reading order or text.");
        return result;
    }

    public static int Main()
    {
        try
        {
            ShxTextLabel label = Label(" 3x2500/5A MCT ", Bounds(671, 1805, 646, 1748));
            Expect(label.Text == "3x2500/5A MCT", "Preserve internal punctuation and spaces.");
            Expect(label.Bounds.left == 646 && label.Bounds.right == 671 &&
                label.Bounds.bottom == 1748 && label.Bounds.top == 1805,
                "Normalize inverted AutoCAD rectangles.");
            ShxTextLabel ignored;
            foreach (string title in new[] { null, "", "Engineer", "AutoCAD SHX",
                "autocad shx text", "AutoCAD SHX Text extra" })
                Expect(!ShxTextReader.TryCreate(title, "PRIVATE COMMENT",
                    Bounds(0, 0, 10, 10), out ignored), "Ignore ordinary comments.");
            foreach (string text in new[] { null, "", " \r\n\t " })
                Expect(!ShxTextReader.TryCreate("AutoCAD SHX Text", text,
                    Bounds(0, 0, 10, 10), out ignored), "Ignore empty SHX originals.");
            foreach (PXC_Rect bounds in new[] {
                Bounds(0, 0, 0, 10), Bounds(0, 0, 10, 0),
                Bounds(Double.NaN, 0, 10, 10), Bounds(0, Double.PositiveInfinity, 10, 10),
                Bounds(0, 0, Double.NegativeInfinity, 10), Bounds(0, 0, 10, Double.NaN) })
                Expect(!ShxTextReader.TryCreate("AutoCAD SHX Text", "A", bounds,
                    out ignored), "Ignore invalid or empty bounds.");

            var labels = new List<ShxTextLabel> { label };
            string picked;
            Expect(ShxTextReader.TryGetText(labels, new PXC_Point { x = 658, y = 1780 },
                out picked) && picked == label.Text, "Click reads the whole SHX label.");
            Expect(ShxTextReader.TryGetText(labels, new PXC_Point { x = 646, y = 1748 },
                out picked), "Click on the label boundary.");
            Expect(!ShxTextReader.TryGetText(labels, new PXC_Point { x = 645, y = 1780 },
                out picked) && picked == null, "Blank clicks do not choose distant text.");
            labels.Add(Label("SMALL", Bounds(652, 1775, 662, 1785)));
            Expect(ShxTextReader.TryGetText(labels, new PXC_Point { x = 658, y = 1780 },
                out picked) && picked == "SMALL", "Use the smallest overlapping label.");
            labels.Add(Label("NEAR", Bounds(656, 1778, 666, 1788)));
            Expect(ShxTextReader.TryGetText(labels, new PXC_Point { x = 660, y = 1783 },
                out picked) && picked == "NEAR", "Break equal-area ties by distance.");
            Expect(!ShxTextReader.TryGetText(new List<ShxTextLabel>(),
                new PXC_Point(), out picked), "An empty SHX page is safe.");

            var regions = new List<RegionGlyph>
            {
                Region("LI-282", 0, Rect(10, 70, 46, 80)),
                Region("LI-058", 1, Rect(80, 10, 116, 20)),
                Region("LI-058", 2, Rect(10, 12, 46, 22))
            };
            RegionPickResult all = Words(regions, Rect(0, 0, 150, 100),
                "LI-058", "LI-058", "LI-282");
            Expect(all.HighlightRects.Length == 3, "Highlight whole SHX label bounds.");
            Words(regions, Rect(150, 100, 0, 0), "LI-058", "LI-058", "LI-282");
            regions.Reverse();
            Words(regions, Rect(0, 0, 150, 100), "LI-058", "LI-058", "LI-282");
            Words(regions, Rect(10, 10, 20, 25));
            RegionPickResult edge = Words(regions, Rect(28, 17, 29, 18), "LI-058");
            Expect(edge.HighlightRects[0].left == 10 && edge.HighlightRects[0].right == 46,
                "The preview shows the complete selected label even for a small box.");
            Words(regions, Rect(0, 0, 0, 100));
            Words(regions, Rect(0, 0, 100, 0));
            Words(regions, Rect(200, 200, 300, 300));
            regions.Add(Region(" \t", 20, Rect(10, 10, 40, 20)));
            regions.Add(Region("ZERO", 21, Rect(10, 10, 10, 20)));
            Words(regions, Rect(0, 0, 150, 100), "LI-058", "LI-058", "LI-282");

            var mixed = new List<RegionGlyph>();
            Native(mixed, "NATIVE", 10, 10);
            mixed.Add(Region("3x2500/5A MCT", 0, Rect(10, 40, 110, 50)));
            Words(mixed, Rect(0, 0, 150, 100), "NATIVE", "3x2500/5A MCT");
            mixed.Add(Region("NATIVE", 1, Rect(10, 10, 46, 20)));
            RegionPickResult deduped = Words(mixed, Rect(0, 0, 150, 100),
                "NATIVE", "3x2500/5A MCT");
            Expect(deduped.HighlightRects.Length == 7,
                "Prefer native per-character highlights for identical overlapping text.");
            Words(mixed, Rect(25, 10, 28, 20), "T");
            mixed.Add(Region("NATIVE", 2, Rect(70, 10, 106, 20)));
            Words(mixed, Rect(0, 0, 150, 100), "NATIVE", "NATIVE", "3x2500/5A MCT");
            mixed.Add(Region("DIFFERENT", 3, Rect(10, 10, 46, 20)));
            Words(mixed, Rect(0, 0, 150, 100), "NATIVE", "DIFFERENT",
                "NATIVE", "3x2500/5A MCT");

            string[][] order = {
                new[] { "A", "B", "C" }, new[] { "B", "A", "C" },
                new[] { "C", "B", "A" }, new[] { "C", "A", "B" }
            };
            for (int rotation = 0; rotation < 4; rotation++)
            {
                var rotated = new List<RegionGlyph>
                {
                    Region("A", 2, Rect(10, 10, 20, 20)),
                    Region("B", 1, Rect(10, 40, 20, 50)),
                    Region("C", 0, Rect(70, 40, 80, 50))
                };
                for (int i = 0; i < rotated.Count; i++)
                {
                    RegionGlyph changed = rotated[i];
                    tagRECT rect = changed.DeviceBounds;
                    if (rotation == 1)
                        changed.DeviceBounds = Rect(-rect.bottom, rect.left, -rect.top, rect.right);
                    else if (rotation == 2)
                        changed.DeviceBounds = Rect(-rect.right, -rect.bottom, -rect.left, -rect.top);
                    else if (rotation == 3)
                        changed.DeviceBounds = Rect(rect.top, -rect.right, rect.bottom, -rect.left);
                    rotated[i] = changed;
                }
                Words(rotated, Rect(-200, -200, 200, 200), order[rotation]);
            }
            foreach (string separator in new[] { "/", "-", "\uff0c", "\u3001", " ", "", "::" })
                Expect(String.Join(separator, all.Fragments) ==
                    "LI-058" + separator + "LI-058" + separator + "LI-282",
                    "SHX fragments use the existing separator semantics.");
            Console.WriteLine("ShxText: " + checks + " checks passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
