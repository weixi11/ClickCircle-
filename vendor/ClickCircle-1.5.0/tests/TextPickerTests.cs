using System;
using System.Collections.Generic;
using ClickCircle;
using PDFXEdit;

internal static class TextPickerTests
{
    private static int checkedCases;

    private static List<TextPicker.Glyph> MakeGlyphs(string text)
    {
        var result = new List<TextPicker.Glyph>();
        double x = 0;
        foreach (char character in text)
        {
            result.Add(new TextPicker.Glyph
            {
                Character = character,
                PageBounds = new PXC_Rect
                {
                    left = x, right = x + 0.6, bottom = 0, top = 1
                },
                LineBounds = new PXC_RectF
                {
                    left = (float)x, right = (float)(x + 0.6), bottom = 0, top = 1
                }
            });
            x += 0.62;
        }
        return result;
    }

    private static void Expect(List<TextPicker.Glyph> glyphs, int hit, string expected)
    {
        string actual;
        bool found = TextPicker.TryGetWord(glyphs, hit, out actual);
        if ((expected != null) != found || actual != expected)
            throw new Exception("Expected " + (expected ?? "<none>") + ", got " + (actual ?? "<none>"));
        ++checkedCases;
    }

    public static int Main()
    {
        try
        {
            Expect(MakeGlyphs("LI-282"), 0, "LI-282");
            Expect(MakeGlyphs("LI-282"), 3, "LI-282");
            Expect(MakeGlyphs("LI-282"), 5, "LI-282");
            Expect(MakeGlyphs("LI-156x8rows"), 5, "LI-156x8rows");
            Expect(MakeGlyphs("\u5e94\u6025\u7167\u660e"), 1, "\u5e94\u6025\u7167\u660e");
            Expect(MakeGlyphs("LI-278 LI-282"), 2, "LI-278");
            Expect(MakeGlyphs("LI-278 LI-282"), 8, "LI-282");
            Expect(MakeGlyphs("LI-278 LI-282"), 6, null);
            Expect(MakeGlyphs("LI-278\tLI-282"), 6, null);
            Expect(MakeGlyphs("LI-282"), -1, null);
            Expect(MakeGlyphs("LI-282"), 6, null);

            List<TextPicker.Glyph> gap = MakeGlyphs("LI-278LI-282");
            for (int i = 6; i < gap.Count; ++i)
            {
                TextPicker.Glyph glyph = gap[i];
                glyph.LineBounds.left += 2;
                glyph.LineBounds.right += 2;
                gap[i] = glyph;
            }
            Expect(gap, 1, "LI-278");
            Expect(gap, 7, "LI-282");

            List<TextPicker.Glyph> otherBaseline = MakeGlyphs("LI-278LI-282");
            for (int i = 6; i < otherBaseline.Count; ++i)
            {
                TextPicker.Glyph glyph = otherBaseline[i];
                glyph.LineBounds.top += 2;
                glyph.LineBounds.bottom += 2;
                otherBaseline[i] = glyph;
            }
            Expect(otherBaseline, 2, "LI-278");
            Expect(otherBaseline, 9, "LI-282");
            Console.WriteLine("TextPicker: " + checkedCases + " cases passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
