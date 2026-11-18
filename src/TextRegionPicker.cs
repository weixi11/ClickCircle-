using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;
using PDFXEdit;

namespace ClickCircle
{
    public struct RegionGlyph
    {
        public uint Index;
        public uint LineIndex;
        public TextPicker.Glyph Glyph;
        public tagRECT DeviceBounds;
        // SHX annotations contain a whole label, with no individual character positions.
        public string LabelText;
    }

    public sealed class RegionPickResult
    {
        public RegionPickResult(string[] fragments, tagRECT[] highlightRects)
        {
            Fragments = fragments ?? new string[0];
            HighlightRects = highlightRects ?? new tagRECT[0];
        }

        public string[] Fragments { get; private set; }
        public tagRECT[] HighlightRects { get; private set; }
    }

    public static class TextRegionPicker
    {
        private sealed class Fragment
        {
            public readonly StringBuilder Text = new StringBuilder();
            public readonly List<tagRECT> Rects = new List<tagRECT>();
            public tagRECT Bounds;
            public uint FirstIndex;
            public double GlyphHeight;

            public double CenterY
            {
                get { return ((double)Bounds.top + Bounds.bottom) / 2; }
            }

            public void Add(RegionGlyph glyph)
            {
                tagRECT rect = Normalize(glyph.DeviceBounds);
                if (Rects.Count == 0)
                {
                    Bounds = rect;
                    FirstIndex = glyph.Index;
                }
                else
                {
                    Bounds.left = Math.Min(Bounds.left, rect.left);
                    Bounds.top = Math.Min(Bounds.top, rect.top);
                    Bounds.right = Math.Max(Bounds.right, rect.right);
                    Bounds.bottom = Math.Max(Bounds.bottom, rect.bottom);
                }
                GlyphHeight += ((double)rect.bottom - rect.top - GlyphHeight) / (Rects.Count + 1);
                Rects.Add(rect);
                if (glyph.LabelText == null)
                    Text.Append(glyph.Glyph.Character);
                else
                    Text.Append(glyph.LabelText);
            }
        }

        private sealed class VisualRow
        {
            public readonly List<Fragment> Fragments = new List<Fragment>();
            public double CenterY;
            public double GlyphHeight;
        }

        public static List<RegionGlyph> Capture(IPXC_Page page, Func<PXC_Rect, tagRECT> toDevice)
        {
            if (page == null) throw new ArgumentNullException("page");
            if (toDevice == null) throw new ArgumentNullException("toDevice");

            var result = new List<RegionGlyph>();
            try { CaptureNative(page.GetText(null, false), toDevice, result); }
            catch (COMException)
            {
                // Keep any valid native glyphs, then try the independent SHX originals.
            }
            uint labelIndex = 0;
            foreach (ShxTextLabel label in ShxTextReader.Read(page))
            {
                result.Add(new RegionGlyph
                {
                    Index = labelIndex++,
                    LabelText = label.Text,
                    DeviceBounds = toDevice(label.Bounds)
                });
            }
            return result;
        }

        private static void CaptureNative(IPXC_PageText text,
            Func<PXC_Rect, tagRECT> toDevice, List<RegionGlyph> result)
        {
            if (text == null) return;
            for (uint index = 0; index < text.CharCount; index++)
            {
                char character = (char)text.get_Char(index);
                if (TextPicker.IsSeparator(character)) continue;
                PXC_Rect bounds = text.get_CharRect(index);
                result.Add(new RegionGlyph
                {
                    Index = index,
                    LineIndex = text.get_CharLineIndex(index),
                    Glyph = new TextPicker.Glyph
                    {
                        Character = character,
                        PageBounds = bounds,
                        LineBounds = text.get_CharBBox(index)
                    },
                    DeviceBounds = toDevice(bounds)
                });
            }
        }

        public static RegionPickResult Select(IList<RegionGlyph> glyphs, tagRECT deviceRect)
        {
            if (glyphs == null) throw new ArgumentNullException("glyphs");
            tagRECT selection = Normalize(deviceRect);
            if (selection.left == selection.right || selection.top == selection.bottom)
                return new RegionPickResult(null, null);

            var selected = new List<RegionGlyph>();
            var fragments = new List<Fragment>();
            foreach (RegionGlyph glyph in glyphs)
            {
                if (glyph.LabelText == null
                    ? TextPicker.IsSeparator(glyph.Glyph.Character)
                    : String.IsNullOrWhiteSpace(glyph.LabelText))
                    continue;
                tagRECT rect = Normalize(glyph.DeviceBounds);
                if (rect.left == rect.right || rect.top == rect.bottom) continue;
                double x = ((double)rect.left + rect.right) / 2;
                double y = ((double)rect.top + rect.bottom) / 2;
                if (x >= selection.left && x <= selection.right &&
                    y >= selection.top && y <= selection.bottom)
                {
                    if (glyph.LabelText == null)
                        selected.Add(glyph);
                    else
                    {
                        var label = new Fragment();
                        label.Add(glyph);
                        fragments.Add(label);
                    }
                }
            }
            // Only suppress a second representation of the same text at the same location.
            // Compare against full native words so a partial selection stays partial.
            if (fragments.Count != 0)
            {
                var nativeGlyphs = new List<RegionGlyph>();
                foreach (RegionGlyph glyph in glyphs)
                    if (glyph.LabelText == null && !TextPicker.IsSeparator(glyph.Glyph.Character))
                        nativeGlyphs.Add(glyph);
                var nativeByText = new Dictionary<string, List<Fragment>>(StringComparer.Ordinal);
                foreach (Fragment native in BuildNativeFragments(nativeGlyphs))
                {
                    string text = native.Text.ToString();
                    List<Fragment> matches;
                    if (!nativeByText.TryGetValue(text, out matches))
                    {
                        matches = new List<Fragment>();
                        nativeByText.Add(text, matches);
                    }
                    matches.Add(native);
                }
                fragments.RemoveAll(delegate(Fragment label)
                {
                    List<Fragment> matches;
                    if (!nativeByText.TryGetValue(label.Text.ToString(), out matches))
                        return false;
                    foreach (Fragment native in matches)
                        if (SameLocation(native.Bounds, label.Bounds))
                            return true;
                    return false;
                });
            }
            fragments.AddRange(BuildNativeFragments(selected));

            // Group visual rows before sorting horizontally; a pairwise tolerance
            // comparator can otherwise violate the sorting contract.
            fragments.Sort(delegate(Fragment a, Fragment b)
            {
                int byY = a.CenterY.CompareTo(b.CenterY);
                if (byY != 0) return byY;
                int byX = a.Bounds.left.CompareTo(b.Bounds.left);
                return byX != 0 ? byX : a.FirstIndex.CompareTo(b.FirstIndex);
            });
            var rows = new List<VisualRow>();
            foreach (Fragment fragment in fragments)
            {
                VisualRow row = rows.Count == 0 ? null : rows[rows.Count - 1];
                if (row == null || Math.Abs(fragment.CenterY - row.CenterY) >
                    Math.Min(fragment.GlyphHeight, row.GlyphHeight) * 0.45)
                {
                    row = new VisualRow
                    {
                        CenterY = fragment.CenterY,
                        GlyphHeight = fragment.GlyphHeight
                    };
                    rows.Add(row);
                }
                row.Fragments.Add(fragment);
            }

            var words = new List<string>();
            var highlights = new List<tagRECT>();
            foreach (VisualRow row in rows)
            {
                row.Fragments.Sort(delegate(Fragment a, Fragment b)
                {
                    int byX = a.Bounds.left.CompareTo(b.Bounds.left);
                    if (byX != 0) return byX;
                    int byY = a.CenterY.CompareTo(b.CenterY);
                    return byY != 0 ? byY : a.FirstIndex.CompareTo(b.FirstIndex);
                });
                foreach (Fragment fragment in row.Fragments)
                {
                    words.Add(fragment.Text.ToString());
                    highlights.AddRange(fragment.Rects);
                }
            }
            return new RegionPickResult(words.ToArray(), highlights.ToArray());
        }

        private static List<Fragment> BuildNativeFragments(List<RegionGlyph> glyphs)
        {
            glyphs.Sort(delegate(RegionGlyph a, RegionGlyph b)
            {
                int byLine = a.LineIndex.CompareTo(b.LineIndex);
                return byLine != 0 ? byLine : a.Index.CompareTo(b.Index);
            });
            var fragments = new List<Fragment>();
            Fragment current = null;
            RegionGlyph previous = default(RegionGlyph);
            foreach (RegionGlyph glyph in glyphs)
            {
                if (current == null || previous.LineIndex != glyph.LineIndex ||
                    (ulong)previous.Index + 1 != glyph.Index ||
                    !TextPicker.IsJoined(previous.Glyph, glyph.Glyph))
                {
                    current = new Fragment();
                    fragments.Add(current);
                }
                current.Add(glyph);
                previous = glyph;
            }
            return fragments;
        }

        private static bool SameLocation(tagRECT a, tagRECT b)
        {
            double overlap = Math.Max(0L, (long)Math.Min(a.right, b.right) -
                Math.Max(a.left, b.left)) * (double)Math.Max(0L,
                (long)Math.Min(a.bottom, b.bottom) - Math.Max(a.top, b.top));
            double areaA = ((double)a.right - a.left) * ((double)a.bottom - a.top);
            double areaB = ((double)b.right - b.left) * ((double)b.bottom - b.top);
            return overlap >= Math.Min(areaA, areaB) * 0.8 &&
                overlap >= Math.Max(areaA, areaB) * 0.5;
        }

        private static tagRECT Normalize(tagRECT rect)
        {
            return new tagRECT
            {
                left = Math.Min(rect.left, rect.right),
                right = Math.Max(rect.left, rect.right),
                top = Math.Min(rect.top, rect.bottom),
                bottom = Math.Max(rect.top, rect.bottom)
            };
        }
    }
}
