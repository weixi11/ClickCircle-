// Test-only plugin loaded beside ClickCircle in an isolated editor.
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;
using ClickCircle;
using PDFXEdit;

namespace ClickCircleTests
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class HostPlugin : IPXV_Plugin
    {
        private IPXV_Inst instance;
        private Timer timer;
        private int checks;
        private readonly StringBuilder report = new StringBuilder();

        public static int Create(uint apiVersion, IntPtr reserved, IntPtr output)
        {
            Marshal.WriteIntPtr(output, Marshal.GetComInterfaceForObject(new HostPlugin(),
                typeof(IPXV_Plugin)));
            return 0;
        }

        public void Setup(PXV_Inst value) { instance = value; }
        public void RegisterExts() { }
        public void FinalizeRegistering() { }
        public void Init()
        {
            timer = new Timer { Interval = 800 };
            timer.Tick += Run;
            timer.Start();
        }
        public void Unload() { if (timer != null) timer.Dispose(); }
        public void ShowPrefsDlg(ulong parent) { }
        public void ImportAdditionalData(IAFS_Name app, IAFS_Name user) { }
        public void ExportAdditionalData(IAFS_Name app, IAFS_Name user) { }
        public void ResetSettings(uint flags) { }
        public Guid _GUID { get { return new Guid("64A5C8D5-E343-4FBA-9C48-D6A6D07A3718"); } }
        public string Name { get { return "ClickCircle SHX host tests"; } }
        public string CopyrightInfo { get { return ""; } }
        public uint version { get { return 1; } }
        public uint VendorID { get { return 0; } }
        public string Description { get { return Name; } }
        public string LegalInfo { get { return ""; } }
        public string Publisher { get { return "ClickCircle tests"; } }
        public uint Features { get { return 0; } }
        public string PresetsCollectionsNames { get { return ""; } }

        private void Expect(bool condition, string message)
        {
            if (!condition) throw new Exception(message);
            checks++;
        }

        private void Run(object sender, EventArgs args)
        {
            timer.Stop();
            string root = Environment.GetEnvironmentVariable("CLICKCIRCLE_SHX_TEST_ROOT");
            if (String.IsNullOrEmpty(root)) return;
            try
            {
                var core = (IPXC_Inst)instance.GetExtension("PXC");
                CheckFixture(core, Path.Combine(root, "tests", "ClickCircle_shx_selection.pdf"));
                string drawing = Environment.GetEnvironmentVariable("CLICKCIRCLE_SHX_TEST_DRAWING");
                if (!String.IsNullOrEmpty(drawing))
                    CheckDrawing(core, drawing);
                string native = Path.Combine(Path.GetDirectoryName(root),
                    "GF LIGHTING LAYOUT OVERALL FLOOR PLAN-20260918-0300_A0(1).pdf");
                if (File.Exists(native))
                    CheckNative(core, native);
                report.AppendLine("PASS checks=" + checks);
            }
            catch (Exception exception) { report.AppendLine("FAIL " + exception); }
            File.WriteAllText(Path.Combine(root, "output", "shx-host-tests.txt"), report.ToString());
        }

        private static tagRECT Device(PXC_Rect bounds, int rotation)
        {
            double left = bounds.left, right = bounds.right;
            double top = -bounds.top, bottom = -bounds.bottom;
            if (rotation == 90)
                return Rect(-bottom, left, -top, right);
            if (rotation == 180)
                return Rect(-right, -bottom, -left, -top);
            if (rotation == 270)
                return Rect(top, -right, bottom, -left);
            return Rect(left, top, right, bottom);
        }

        private static tagRECT Rect(double left, double top, double right, double bottom)
        {
            return new tagRECT
            {
                left = (int)Math.Floor(left), top = (int)Math.Floor(top),
                right = (int)Math.Ceiling(right), bottom = (int)Math.Ceiling(bottom)
            };
        }

        private void CheckFixture(IPXC_Inst core, string path)
        {
            IPXC_Document document = core.OpenDocumentFromFile(path, null, null, 0, 0);
            try
            {
                for (uint pageIndex = 0; pageIndex < 4; pageIndex++)
                {
                    IPXC_Page page = document.Pages[pageIndex];
                    uint before = page.GetAnnotsCount();
                    List<ShxTextLabel> labels = ShxTextReader.Read(page);
                    Expect(labels.Count == 5, "Only five nonempty SHX labels per page.");
                    foreach (ShxTextLabel label in labels)
                    {
                        string text;
                        PXC_Rect b = label.Bounds;
                        Expect(TextPicker.TryGetWord(page, new PXC_Point
                        {
                            x = (b.left + b.right) / 2, y = (b.bottom + b.top) / 2
                        }, out text) && text == label.Text,
                            "Actual SDK click: " + label.Text + " page=" + pageIndex);
                    }
                    string ignored;
                    Expect(!TextPicker.TryGetWord(page, new PXC_Point { x = 350, y = 240 },
                        out ignored), "Never pick ordinary comment contents.");
                    Expect(!TextPicker.TryGetWord(page, new PXC_Point { x = 100, y = 190 },
                        out ignored), "Empty SHX contents are not picked.");
                    for (int viewRotation = 0; viewRotation < 360; viewRotation += 90)
                    {
                        int displayRotation = (page.Rotation + viewRotation) % 360;
                        List<RegionGlyph> captured = TextRegionPicker.Capture(page,
                            delegate(PXC_Rect b) { return Device(b, displayRotation); });
                        RegionPickResult selected = TextRegionPicker.Select(captured,
                            Device(new PXC_Rect { left = 75, right = 145,
                                bottom = 230, top = 345 }, displayRotation));
                        string expected = displayRotation == 0 || displayRotation == 270
                            ? "LI-058/LI-058/LI-282" : "LI-282/LI-058/LI-058";
                        Expect(String.Join("/", selected.Fragments) == expected,
                            "SDK SHX ordering at page/view rotation " + page.Rotation + "/" +
                            viewRotation + ": " + String.Join("/", selected.Fragments));
                        Expect(selected.HighlightRects.Length == 3,
                            "Three complete SHX highlights.");
                        RegionPickResult mixed = TextRegionPicker.Select(captured,
                            Device(new PXC_Rect { left = 75, right = 480,
                                bottom = 225, top = 345 }, displayRotation));
                        Expect(mixed.Fragments.Length == 5,
                            "Mixed content includes four SHX and one native label, without duplicates.");
                        Expect(mixed.HighlightRects.Length == 10,
                            "Native characters retain their individual highlights.");
                        Expect(Array.IndexOf(mixed.Fragments, "IGNORE-COMMENT") < 0,
                            "Ordinary comments never enter box selection.");
                    }
                    Expect(page.GetAnnotsCount() == before,
                        "Picking and preview never create or alter annotations.");
                    report.AppendLine("Fixture page=" + pageIndex + " rotation=" +
                        page.Rotation + " nativeChars=" + page.GetText(null, false).CharCount +
                        " SHX=" + labels.Count + " annots=" + before);
                }
            }
            finally { document.Close(); }
        }

        private void CheckDrawing(IPXC_Inst core, string path)
        {
            IPXC_Document document = core.OpenDocumentFromFile(path, null, null, 0, 0);
            try
            {
                IPXC_Page page = document.Pages[0];
                uint before = page.GetAnnotsCount();
                List<ShxTextLabel> labels = ShxTextReader.Read(page);
                Expect(labels.Count == 509, "Read all 509 SHX originals from the real drawing.");
                int mct = 0;
                int clickMatches = 0;
                int reachable = 0;
                foreach (ShxTextLabel label in labels)
                {
                    string text;
                    PXC_Rect b = label.Bounds;
                    bool found = TextPicker.TryGetWord(page, new PXC_Point
                    {
                        x = (b.left + b.right) / 2, y = (b.bottom + b.top) / 2
                    }, out text);
                    if (found && text == label.Text) clickMatches++;
                    bool canPick = found && text == label.Text;
                    if (!canPick)
                    {
                        report.AppendLine("Overlapping center expected=[" + label.Text +
                            "] picked=[" + text + "]");
                        foreach (double x in new[] { 0.1, 0.5, 0.9 })
                            foreach (double y in new[] { 0.1, 0.5, 0.9 })
                                if (TextPicker.TryGetWord(page, new PXC_Point
                                {
                                    x = b.left + (b.right - b.left) * x,
                                    y = b.bottom + (b.top - b.bottom) * y
                                }, out text) && text == label.Text)
                                    canPick = true;
                    }
                    if (canPick) reachable++;
                    Expect(canPick, "Every real SHX original has a pickable point: " + label.Text);
                    if (label.Text == "3x2500/5A MCT")
                    {
                        mct++;
                        Expect(found && text == label.Text, "Pick the real MCT label.");
                    }
                }
                Expect(mct == 6, "Keep all six distinct MCT labels.");
                List<RegionGlyph> captured = TextRegionPicker.Capture(page,
                    delegate(PXC_Rect b) { return Device(b, 270); });
                int capturedLabels = 0;
                foreach (RegionGlyph glyph in captured)
                    if (glyph.LabelText != null) capturedLabels++;
                Expect(capturedLabels == 509, "The box snapshot includes all 509 SHX labels.");
                RegionPickResult selected = TextRegionPicker.Select(captured,
                    Rect(-10000, -10000, 10000, 10000));
                int selectedMct = 0;
                foreach (string fragment in selected.Fragments)
                    if (fragment == "3x2500/5A MCT") selectedMct++;
                Expect(selectedMct == 6, "Box selection preserves all repeated MCT labels.");
                Expect(page.GetAnnotsCount() == before && before == 513,
                    "No new annotations on the real drawing.");
                report.AppendLine("Drawing SHX=" + labels.Count + " centerClicks=" + clickMatches +
                    "/" + labels.Count + " reachable=" + reachable + " MCT=" + mct +
                    " annotsUnchanged=" + before);
            }
            finally { document.Close(); }
        }

        private void CheckNative(IPXC_Inst core, string path)
        {
            IPXC_Document document = core.OpenDocumentFromFile(path, null, null, 0, 0);
            try
            {
                IPXC_Page page = document.Pages[0];
                Expect(ShxTextReader.Read(page).Count == 0, "Native drawing has no SHX originals.");
                IPXC_PageText native = page.GetText(null, false);
                var content = new StringBuilder();
                for (uint i = 0; i < native.CharCount; i++) content.Append((char)native.get_Char(i));
                int index = content.ToString().IndexOf("LI-282", StringComparison.Ordinal);
                Expect(index >= 0, "Native regression drawing contains LI-282.");
                PXC_Rect b = native.get_CharRect((uint)index);
                string picked;
                Expect(TextPicker.TryGetWord(page, new PXC_Point
                {
                    x = (b.left + b.right) / 2, y = (b.bottom + b.top) / 2
                }, out picked) && picked == "LI-282", "Native picking still returns LI-282.");
                List<RegionGlyph> glyphs = TextRegionPicker.Capture(page,
                    delegate(PXC_Rect bounds) { return Device(bounds, page.Rotation); });
                Expect(glyphs.Count > 10000, "Native region capture remains populated.");
                report.AppendLine("Native drawing chars=" + native.CharCount +
                    " glyphs=" + glyphs.Count + " picked=" + picked);
            }
            finally { document.Close(); }
        }
    }
}
