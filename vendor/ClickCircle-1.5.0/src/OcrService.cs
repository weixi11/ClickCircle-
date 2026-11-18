using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;

namespace ClickCircle
{
    public sealed class OcrLanguage
    {
        public string Tag { get; private set; }
        public string Name { get; private set; }
        public OcrLanguage(string tag, string name) { Tag = tag; Name = name; }
        public override string ToString() { return Tag.Length == 0 ? Name : Name + " (" + Tag + ")"; }
    }

    public sealed class OcrResult
    {
        public string[] Fragments = new string[0];
        public readonly List<OcrLanguage> Languages = new List<OcrLanguage>();
        public string Language = String.Empty;
        public string Error;
    }

    public static class OcrService
    {
        private sealed class Word
        {
            internal string Text;
            internal int Line;
            internal double X, Y, Width, Height;
        }

        public static OcrResult Recognize(Bitmap bitmap, string language, CancellationToken cancel)
        {
            string directory = Path.Combine(Path.GetTempPath(), "ClickCircle-OCR-" +
                Guid.NewGuid().ToString("N"));
            try
            {
                cancel.ThrowIfCancellationRequested();
                Directory.CreateDirectory(directory);
                string image = Path.Combine(directory, "region.png");
                string helper = Path.Combine(directory, "recognize.ps1");
                bitmap.Save(image, ImageFormat.Png);
                using (Stream resource = typeof(OcrService).Assembly.GetManifestResourceStream(
                    "ClickCircle.Ocr.ps1"))
                using (Stream output = File.Create(helper))
                {
                    if (resource == null) throw new IOException("缺少区域识别组件");
                    resource.CopyTo(output);
                }
                string root = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
                string powershell = Path.Combine(root, @"System32\WindowsPowerShell\v1.0\powershell.exe");
                if (!File.Exists(powershell)) throw new IOException("找不到 Windows PowerShell");
                if (!IsLanguageTag(language)) throw new ArgumentException("无效的识别语言");
                ProcessStartInfo start = new ProcessStartInfo {
                    FileName = powershell,
                    Arguments = "-NoProfile -NonInteractive -ExecutionPolicy Bypass -File \"" +
                        helper + "\" -Path \"" + image + "\" -Language \"" + (language ?? "") + "\"",
                    UseShellExecute = false, CreateNoWindow = true,
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    StandardOutputEncoding = Encoding.UTF8, StandardErrorEncoding = Encoding.UTF8
                };
                using (Process process = new Process { StartInfo = start })
                {
                    StringBuilder text = new StringBuilder();
                    bool overflow = false;
                    process.OutputDataReceived += delegate(object sender, DataReceivedEventArgs e) {
                        if (e.Data == null) return;
                        lock (text)
                        {
                            if (text.Length + e.Data.Length > 4 * 1024 * 1024) overflow = true;
                            else if (!overflow) text.AppendLine(e.Data);
                        }
                    };
                    process.ErrorDataReceived += delegate { };
                    cancel.ThrowIfCancellationRequested();
                    process.Start();
                    try
                    {
                        process.BeginOutputReadLine();
                        process.BeginErrorReadLine();
                        Stopwatch clock = Stopwatch.StartNew();
                        while (!process.WaitForExit(100))
                        {
                            cancel.ThrowIfCancellationRequested();
                            if (clock.ElapsedMilliseconds > 30000)
                                throw new TimeoutException("识别超时，请缩小区域后重试");
                        }
                        process.WaitForExit();
                        cancel.ThrowIfCancellationRequested();
                        if (overflow) throw new InvalidDataException("识别内容过多，请缩小区域");
                        OcrResult result = Parse(text.ToString());
                        if (process.ExitCode != 0 && result.Error == null)
                            throw new IOException("本地识别进程未正常完成");
                        return result;
                    }
                    finally
                    {
                        // Terminate only this job's helper, never the editor.
                        if (!process.HasExited)
                        {
                            try { process.Kill(); process.WaitForExit(2000); }
                            catch (InvalidOperationException) { }
                        }
                    }
                }
            }
            finally
            {
                // Remove known temporary files without a recursive directory operation.
                foreach (string name in new[] { "region.png", "recognize.ps1" })
                    try { File.Delete(Path.Combine(directory, name)); }
                    catch (IOException) { }
                    catch (UnauthorizedAccessException) { }
                try { if (Directory.Exists(directory)) Directory.Delete(directory, false); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
            }
        }

        private static bool IsLanguageTag(string language)
        {
            if (language == null) return true;
            if (language.Length > 64) return false;
            foreach (char c in language)
                if (!((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') ||
                    (c >= '0' && c <= '9') || c == '-')) return false;
            return true;
        }

        public static OcrResult Parse(string output)
        {
            if (output == null || output.Length > 4 * 1024 * 1024)
                throw new InvalidDataException("识别结果无效");
            OcrResult result = new OcrResult();
            List<Word> words = new List<Word>();
            bool header = false, complete = false;
            foreach (string raw in output.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0) continue;
                string[] fields = line.Split('\t');
                if (line == "CCOCR\t1") { header = true; continue; }
                if (fields[0] == "ERROR" && fields.Length == 2)
                {
                    result.Error = FriendlyError(Decode(fields[1]));
                    continue;
                }
                if (!header || complete) throw new InvalidDataException("识别结果格式不完整");
                if (line == "END") { complete = true; continue; }
                if (fields[0] == "LANG" && fields.Length == 3)
                {
                    string tag = Decode(fields[1]);
                    if (!IsLanguageTag(tag) || tag.Length == 0)
                        throw new InvalidDataException("识别语言无效");
                    result.Languages.Add(new OcrLanguage(tag, Decode(fields[2])));
                }
                else if (fields[0] == "USED" && fields.Length == 2)
                    result.Language = Decode(fields[1]);
                else if (fields[0] == "WORD" && fields.Length == 7)
                {
                    Word word = new Word { Text = Decode(fields[2]) };
                    if (!Int32.TryParse(fields[1], NumberStyles.None, CultureInfo.InvariantCulture,
                            out word.Line) || word.Line < 0)
                        throw new InvalidDataException("识别行号无效");
                    word.X = Number(fields[3]); word.Y = Number(fields[4]);
                    word.Width = Number(fields[5]); word.Height = Number(fields[6]);
                    if (word.Width <= 0 || word.Height <= 0 || words.Count >= 20000)
                        throw new InvalidDataException("识别文字范围无效");
                    if (!String.IsNullOrWhiteSpace(word.Text)) words.Add(word);
                }
                else throw new InvalidDataException("识别结果格式无效");
            }
            if (result.Error != null) return result;
            if (!header || !complete) throw new InvalidDataException("识别未完成，请重试");
            result.Fragments = Fragments(words);
            return result;
        }

        private static double Number(string value)
        {
            double number;
            if (!Double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out number) ||
                Double.IsNaN(number) || Double.IsInfinity(number) || number < 0 || number > 100000)
                throw new InvalidDataException("识别文字坐标无效");
            return number;
        }

        private static string Decode(string value)
        {
            try { return new UTF8Encoding(false, true).GetString(Convert.FromBase64String(value)); }
            catch (Exception exception)
            {
                if (!(exception is FormatException) && !(exception is DecoderFallbackException)) throw;
                throw new InvalidDataException("识别文字编码无效", exception);
            }
        }

        private static string FriendlyError(string error)
        {
            if (error == "NO_LANGUAGE") return "没有可用的本地 OCR 语言，请安装 Windows 文字识别语言";
            if (error == "LANGUAGE_UNAVAILABLE") return "所选识别语言不可用，请选择其他语言";
            if (error == "IMAGE_TOO_LARGE") return "识别区域过大，请缩小区域";
            return "本地识别失败：" + error;
        }

        private static string[] Fragments(List<Word> words)
        {
            Dictionary<int, List<Word>> lines = new Dictionary<int, List<Word>>();
            foreach (Word word in words)
            {
                List<Word> row;
                if (!lines.TryGetValue(word.Line, out row))
                    lines.Add(word.Line, row = new List<Word>());
                row.Add(word);
            }
            List<List<Word>> ordered = new List<List<Word>>(lines.Values);
            foreach (List<Word> line in ordered)
                line.Sort(delegate(Word a, Word b) { return a.X.CompareTo(b.X); });
            ordered.Sort(delegate(List<Word> a, List<Word> b) {
                double ay = Double.MaxValue, by = Double.MaxValue;
                foreach (Word word in a) ay = Math.Min(ay, word.Y);
                foreach (Word word in b) by = Math.Min(by, word.Y);
                int y = ay.CompareTo(by);
                return y != 0 ? y : a[0].X.CompareTo(b[0].X);
            });
            List<string> fragments = new List<string>();
            foreach (List<Word> line in ordered)
            {
                StringBuilder text = new StringBuilder();
                Word previous = null;
                foreach (Word word in line)
                {
                    double gap = previous == null ? 0 : word.X - previous.X - previous.Width;
                    if (previous != null && gap > Math.Min(previous.Height, word.Height) * 0.8)
                    {
                        fragments.Add(text.ToString());
                        text.Length = 0;
                    }
                    if (text.Length != 0) text.Append(' ');
                    text.Append(word.Text);
                    previous = word;
                }
                if (text.Length != 0) fragments.Add(text.ToString());
            }
            return fragments.ToArray();
        }

        public static string[] ReviewFragments(string text)
        {
            List<string> fragments = new List<string>();
            foreach (string raw in (text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
            {
                string fragment = raw.Trim();
                if (fragment.Length != 0) fragments.Add(fragment);
            }
            return fragments.ToArray();
        }
    }
}
