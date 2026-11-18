using System;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using ClickCircle;

internal static class OcrTests
{
    private static int checks;
    private static void Expect(bool condition, string description)
    {
        if (!condition) throw new Exception(description);
        checks++;
    }
    private static string B64(string text) { return Convert.ToBase64String(Encoding.UTF8.GetBytes(text)); }
    private static string Word(int line, string text, string geometry)
    {
        return "WORD\t" + line + "\t" + B64(text) + "\t" + geometry + "\n";
    }
    private static OcrResult Read(string words) { return OcrService.Parse("CCOCR\t1\n" + words + "END\n"); }
    private static void Invalid(string output)
    {
        try { OcrService.Parse(output); }
        catch (InvalidDataException) { checks++; return; }
        throw new Exception("Invalid OCR response was accepted: " + output);
    }
    private static void Parsing()
    {
        OcrResult result = Read(
            Word(7, "LI-282", "10\t80\t60\t20") +
            Word(0, "LI-058", "200\t10\t60\t20") +
            Word(0, "LI-058", "10\t10\t60\t20"));
        Expect(String.Join("/", result.Fragments) == "LI-058/LI-058/LI-282",
            "Visual order and repeated identifiers survive.");
        Expect(Read(Word(0, "3x2500/5A", "10\t10\t100\t20") +
            Word(0, "MCT", "115\t10\t40\t20")).Fragments[0] == "3x2500/5A MCT",
            "Keep an actual word boundary within a label.");
        string exact = "O0-I1/\u5e94\u6025\u7167\u660e";
        Expect(Read(Word(0, exact, "1\t1\t50\t20")).Fragments[0] == exact,
            "Never silently replace ambiguous characters or punctuation.");
        Expect(Read("").Fragments.Length == 0, "Empty response remains empty.");
        result = Read("LANG\t" + B64("zh-Hans-CN") + "\t" + B64("\u7b80\u4f53\u4e2d\u6587") +
            "\nUSED\t" + B64("zh-Hans-CN") + "\n");
        Expect(result.Language == "zh-Hans-CN" && result.Languages.Count == 1,
            "Installed language inventory is decoded.");
        result = OcrService.Parse("CCOCR\t1\n" + Word(0, "PARTIAL", "1\t1\t10\t20") +
            "ERROR\t" + B64("NO_LANGUAGE") + "\n");
        Expect(result.Error != null && result.Fragments.Length == 0,
            "A failed job never exposes a partial subject.");
        foreach (string code in new[] { "NO_LANGUAGE", "LANGUAGE_UNAVAILABLE", "IMAGE_TOO_LARGE", "OTHER" })
            Expect(OcrService.Parse("ERROR\t" + B64(code)).Error.Length > 0,
                "Actionable error: " + code);
        Invalid("");
        Invalid("CCOCR\t2\nEND");
        Invalid("CCOCR\t1\n" + Word(0, "PARTIAL", "1\t1\t10\t20"));
        Invalid("CCOCR\t1\nWORD\t0\t%%%\t1\t1\t1\t1\nEND");
        foreach (string number in new[] { "NaN", "Infinity", "-1", "100001", "bad" })
            Invalid("CCOCR\t1\n" + Word(0, "INVALID", number + "\t1\t10\t20") + "END");
        Invalid("CCOCR\t1\n" + Word(0, "ZERO", "1\t1\t0\t20") + "END");
        Invalid("CCOCR\t1\n" + Word(-1, "INVALID", "1\t1\t10\t20") + "END");
        Invalid("CCOCR\t1\nEND\nUNKNOWN");
        string[] reviewed = OcrService.ReviewFragments(" LI-058\r\n\r\nLI-058\nLI-282\r3x2500/5A MCT ");
        Expect(String.Join("/", reviewed) == "LI-058/LI-058/LI-282/3x2500/5A MCT",
            "Manual correction retains duplicates and internal spaces.");
        Expect(OcrService.ReviewFragments(" \r\n\t").Length == 0, "Blank review cannot be applied.");
    }
    private static void RenderLimits()
    {
        foreach (double width in new[] { 1.0, 20, 200, 2000, 20000 })
            foreach (double height in new[] { 1.0, 20, 200, 2000, 20000 })
            {
                Size size = OcrRegionRenderer.RasterSize(width, height);
                Expect(size.Width > 0 && size.Height > 0 && size.Width <= 2400 && size.Height <= 2400,
                    "Raster stays bounded for large drawings.");
                Expect(Math.Abs((double)size.Width / size.Height - width / height) <=
                    (width / height) / size.Height + 1.1,
                    "Raster rounding is bounded.");
            }
        foreach (double value in new[] { 0.0, -1, Double.NaN, Double.PositiveInfinity })
        {
            try { OcrRegionRenderer.RasterSize(value, 20); }
            catch (ArgumentException) { checks++; continue; }
            throw new Exception("Invalid raster extent accepted.");
        }
    }
    private static void Review()
    {
        using (Bitmap bitmap = new Bitmap(300, 100))
        foreach (string separator in new[] { "/", "-", "\uff0c", "\u3001", " ", "", " :: " })
        using (OcrReviewDialog dialog = new OcrReviewDialog(bitmap, separator))
        {
            TextBox fragments = (TextBox)dialog.Controls.Find("OcrFragments", true)[0];
            TextBox subject = (TextBox)dialog.Controls.Find("OcrSubject", true)[0];
            Button accept = (Button)dialog.Controls.Find("OcrAccept", true)[0];
            Expect(!accept.Enabled && dialog.ConfirmedFragments == null, "No implicit confirmation.");
            fragments.Text = "LI-058\r\nLI-058\r\nLI-282";
            Expect(accept.Enabled, "A nonempty reviewed result can be confirmed.");
            Expect(subject.Text == "LI-058" + separator + "LI-058" + separator + "LI-282",
                "Review preview uses exact separator.");
            foreach (Size size in new[] { new Size(760, 566), new Size(600, 480) })
            {
                dialog.ClientSize = size;
                dialog.PerformLayout();
                foreach (Control child in dialog.Controls) child.PerformLayout();
                accept.Parent.PerformLayout();
                Expect(accept.Width >= 100 && accept.Height >= 28 &&
                    accept.Bounds.Bottom <= accept.Parent.ClientSize.Height &&
                    accept.Bounds.Right <= accept.Parent.ClientSize.Width,
                    "Confirm command stays visible at the minimum dialog size.");
                Expect(accept.Parent.Parent == dialog.Controls[0],
                    "Footer stays in the dialog layout.");
            }
            fragments.Text = "MANUAL-O0-I1";
            Expect(subject.Text == "MANUAL-O0-I1", "Manual fixes update the preview.");
            fragments.Text = "\r\n ";
            Expect(!accept.Enabled, "Blank editing disables confirmation.");
        }
        using (StampPanel panel = new StampPanel())
            Expect(panel.Controls.Find("OcrPick", true).Length == 1, "Explicit OCR entry exists.");
    }
    private static void Live(string directory)
    {
        Directory.CreateDirectory(directory);
        using (Bitmap bitmap = new Bitmap(1000, 460))
        {
            using (Graphics graphics = Graphics.FromImage(bitmap))
            using (Font font = new Font("Arial", 36, FontStyle.Regular, GraphicsUnit.Pixel))
            {
                graphics.Clear(Color.White);
                graphics.DrawString("LI-058", font, Brushes.Black, 60, 55);
                graphics.DrawString("LI-058", font, Brushes.Black, 400, 55);
                graphics.DrawString("LI-282", font, Brushes.Black, 60, 190);
            }
            bitmap.Save(Path.Combine(directory, "ocr-live-input.png"));
            OcrResult result = OcrService.Recognize(bitmap, "", CancellationToken.None);
            File.WriteAllText(Path.Combine(directory, "ocr-live-result.txt"),
                "Language=" + result.Language + "\nError=" + result.Error + "\n" +
                String.Join("\n", result.Fragments), Encoding.UTF8);
            Expect(result.Error == null && result.Fragments.Length >= 3, "Real local OCR returns labels.");
            int repeats = 0;
            foreach (string text in result.Fragments) if (text == "LI-058") repeats++;
            Expect(repeats == 2 && Array.IndexOf(result.Fragments, "LI-282") >= 0,
                "Real OCR keeps both repeated LI-058 and LI-282.");
            using (CancellationTokenSource cancel = new CancellationTokenSource())
            {
                cancel.CancelAfter(200);
                try { OcrService.Recognize(bitmap, "", cancel.Token); }
                catch (OperationCanceledException) { checks++; return; }
                throw new Exception("Live OCR cancellation was ignored.");
            }
        }
    }
    [STAThread]
    private static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles();
            Parsing(); RenderLimits(); Review();
            if (args.Length == 2 && args[0] == "--live") Live(args[1]);
            Console.WriteLine("OCR: " + checks + " checks passed.");
            return 0;
        }
        catch (Exception exception) { Console.Error.WriteLine(exception); return 1; }
    }
}
