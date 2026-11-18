// Test-only read-only checks in an isolated editor.
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ClickCircle;
using PDFXEdit;

namespace ClickCircleOcrTests
{
    [ComVisible(true)]
    [ClassInterface(ClassInterfaceType.None)]
    public sealed class HostPlugin : IPXV_Plugin
    {
        private IPXV_Inst instance;
        private System.Windows.Forms.Timer timer;
        private int checks;
        private readonly StringBuilder report = new StringBuilder();
        public static int Create(uint apiVersion, IntPtr reserved, IntPtr output)
        {
            Marshal.WriteIntPtr(output, Marshal.GetComInterfaceForObject(new HostPlugin(), typeof(IPXV_Plugin)));
            return 0;
        }
        public void Setup(PXV_Inst value) { instance = value; }
        public void RegisterExts() { }
        public void FinalizeRegistering() { }
        public void Init()
        {
            timer = new System.Windows.Forms.Timer { Interval = 800 };
            timer.Tick += Run;
            timer.Start();
        }
        public void Unload() { if (timer != null) timer.Dispose(); }
        public void ShowPrefsDlg(ulong parent) { }
        public void ImportAdditionalData(IAFS_Name app, IAFS_Name user) { }
        public void ExportAdditionalData(IAFS_Name app, IAFS_Name user) { }
        public void ResetSettings(uint flags) { }
        public Guid _GUID { get { return new Guid("D967BDE2-92F1-4058-8271-EC4F2C9375A2"); } }
        public string Name { get { return "ClickCircle OCR host tests"; } }
        public string CopyrightInfo { get { return ""; } }
        public uint version { get { return 1; } }
        public uint VendorID { get { return 0; } }
        public string Description { get { return Name; } }
        public string LegalInfo { get { return ""; } }
        public string Publisher { get { return "ClickCircle tests"; } }
        public uint Features { get { return 0; } }
        public string PresetsCollectionsNames { get { return ""; } }
        private void Expect(bool value, string description)
        {
            if (!value) throw new Exception(description);
            checks++;
        }
        private static string Hash(Bitmap bitmap)
        {
            Rectangle area = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = bitmap.LockBits(area, ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            try
            {
                byte[] pixels = new byte[Math.Abs(data.Stride) * bitmap.Height];
                Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
                using (SHA256 hash = SHA256.Create())
                    return Convert.ToBase64String(hash.ComputeHash(pixels));
            }
            finally { bitmap.UnlockBits(data); }
        }
        private void Run(object sender, EventArgs args)
        {
            timer.Stop();
            string root = Environment.GetEnvironmentVariable("CLICKCIRCLE_OCR_TEST_ROOT");
            if (String.IsNullOrEmpty(root)) return;
            string output = Path.Combine(root, "output", "ocr-validation");
            Directory.CreateDirectory(output);
            IPXC_Document document = null;
            try
            {
                IPXC_Inst core = (IPXC_Inst)instance.GetExtension("PXC");
                document = core.OpenDocumentFromFile(
                    Path.Combine(root, "tests", "ClickCircle_ocr_selection.pdf"), null, null, 0, 0);
                PXC_Rect region = new PXC_Rect { left = 50, right = 550, bottom = 180, top = 410 };
                using (Bitmap baseline = OcrRegionRenderer.Render(instance, document.Pages[0], region, 0))
                {
                    baseline.Save(Path.Combine(output, "ocr-host-region.png"));
                    for (uint index = 0; index < 4; index++)
                    {
                        IPXC_Page page = document.Pages[index];
                        uint before = page.GetAnnotsCount();
                        Expect(page.GetText(null, false).CharCount == 0, "Fixture has no PDF text layer.");
                        Expect(ShxTextReader.Read(page).Count == 0, "Fixture has no SHX originals.");
                        for (int view = 0; view < 360; view += 90)
                        {
                            int rotation = (page.Rotation + view) % 360;
                            using (Bitmap actual = OcrRegionRenderer.Render(instance, page, region, rotation))
                            using (Bitmap expected = new Bitmap(baseline))
                            {
                                if (rotation == 90) expected.RotateFlip(RotateFlipType.Rotate90FlipNone);
                                if (rotation == 180) expected.RotateFlip(RotateFlipType.Rotate180FlipNone);
                                if (rotation == 270) expected.RotateFlip(RotateFlipType.Rotate270FlipNone);
                                Expect(actual.Size == expected.Size, "Region dimensions follow display rotation.");
                                Expect(Hash(actual) == Hash(expected), "Region pixels follow page/view rotation.");
                            }
                        }
                        Expect(page.GetAnnotsCount() == before, "Rendering never modifies annotations.");
                        report.AppendLine("Page=" + index + " nativeRotation=" + page.Rotation +
                            " viewRotations=0,90,180,270 pixelChecks=PASS annots=" + before);
                    }
                    OcrResult result = OcrService.Recognize(baseline, "", CancellationToken.None);
                    Expect(result.Error == null, "Local OCR succeeds on the host-rendered region.");
                    report.AppendLine("Host OCR language=" + result.Language +
                        " fragments=" + String.Join("/", result.Fragments));
                    Expect(String.Join("/", result.Fragments) == "LI-058/LI-058/LI-282",
                        "Host-rendered OCR preserves identifiers and duplicate order.");
                }
                using (Bitmap blank = OcrRegionRenderer.Render(instance, document.Pages[0],
                    new PXC_Rect { left = 600, right = 700, bottom = 20, top = 100 }, 0))
                {
                    OcrResult result = OcrService.Recognize(blank, "", CancellationToken.None);
                    Expect(result.Error == null && result.Fragments.Length == 0, "Blank crop has no invented words.");
                }
                report.AppendLine("PASS checks=" + checks);
            }
            catch (Exception exception) { report.AppendLine("FAIL " + exception); }
            finally { if (document != null) document.Close(); }
            File.WriteAllText(Path.Combine(output, "ocr-host-tests.txt"), report.ToString(), Encoding.UTF8);
        }
    }
}
