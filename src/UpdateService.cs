using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace ClickCircle
{
    /// <summary>
    /// 基于 GitHub 公共仓库的版本检查与更新包下载。
    /// 只读公开数据，不携带任何凭据；下载完成后由外部批处理等编辑器退出再替换文件。
    /// </summary>
    public static class UpdateService
    {
        public const string Repository = "weixi11/ClickCircle-";
        private const string ApiUrl = "https://api.github.com/repos/" + Repository;
        private const string RawUrl = "https://raw.githubusercontent.com/" + Repository + "/";
        private const string VersionFileUrl = RawUrl + "main/version.txt";

        public static readonly string[] FileNames =
            { "ClickCircle.pvp", "ClickCircle.Core.dll", "PDFXEdit.dll" };

        private static readonly Regex TagNamePattern =
            new Regex("\"name\"\\s*:\\s*\"([^\"]+)\"", RegexOptions.Compiled);
        private static readonly Regex VersionPattern =
            new Regex("^[vV]?(\\d+)\\.(\\d+)\\.(\\d+)$", RegexOptions.Compiled);

        /// <summary>把 "v1.6.6" 解析成三段数字；失败返回 false。</summary>
        public static bool TryParseVersion(string text, out int major, out int minor, out int patch)
        {
            major = 0; minor = 0; patch = 0;
            if (String.IsNullOrWhiteSpace(text))
                return false;
            Match match = VersionPattern.Match(text.Trim());
            if (!match.Success)
                return false;
            major = Int32.Parse(match.Groups[1].Value);
            minor = Int32.Parse(match.Groups[2].Value);
            patch = Int32.Parse(match.Groups[3].Value);
            return true;
        }

        /// <summary>比较两个版本；left 大于 right 返回正数，相等返回 0。</summary>
        public static int CompareVersions(string left, string right)
        {
            int a, b, c, d, e, f;
            bool hasLeft = TryParseVersion(left, out a, out b, out c);
            bool hasRight = TryParseVersion(right, out d, out e, out f);
            if (!hasLeft && !hasRight) return 0;
            if (!hasLeft) return -1;
            if (!hasRight) return 1;
            if (a != d) return a < d ? -1 : 1;
            if (b != e) return b < e ? -1 : 1;
            if (c != f) return c < f ? -1 : 1;
            return 0;
        }

        /// <summary>从 tags API 的 JSON 中挑出版本号最大的标签；没有可用标签返回 null。</summary>
        public static string FindLatestTag(string json)
        {
            if (String.IsNullOrEmpty(json))
                return null;
            string best = null;
            foreach (Match match in TagNamePattern.Matches(json))
            {
                string name = match.Groups[1].Value;
                if (!VersionPattern.IsMatch(name))
                    continue;
                if (best == null || CompareVersions(name, best) > 0)
                    best = name;
            }
            return best;
        }

        /// <summary>从 version.txt 里取出版本标签；忽略空行、注释和 BOM。</summary>
        public static string ParseVersionFile(string text)
        {
            if (String.IsNullOrEmpty(text))
                return null;
            foreach (string rawLine in text.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                string line = rawLine.Trim().TrimStart('\uFEFF');
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;
                return VersionPattern.IsMatch(line) ? line : null;
            }
            return null;
        }

        /// <summary>
        /// 查询最新版本：优先读 raw 上的 version.txt（不受 API 限流），失败再回退到 tags API。
        /// </summary>
        public static string GetLatestTag(out string error)
        {
            string text = GetText(VersionFileUrl, out error);
            if (text != null)
            {
                string tag = ParseVersionFile(text);
                if (tag != null)
                    return tag;
                error = "version.txt 内容不是有效版本号";
                return null;
            }
            string json = GetText(ApiUrl + "/tags?per_page=100", out error);
            if (json == null)
                return null;
            string latest = FindLatestTag(json);
            if (latest == null)
                error = "没有可用的版本标签";
            return latest;
        }

        /// <summary>把指定标签下的三个发行文件下载到 directory。</summary>
        public static bool Download(string tag, string directory, out string error)
        {
            error = null;
            if (String.IsNullOrWhiteSpace(tag))
            {
                error = "版本号为空";
                return false;
            }
            Directory.CreateDirectory(directory);
            foreach (string name in FileNames)
            {
                string url = RawUrl + Uri.EscapeDataString(tag) + "/dist/" + Uri.EscapeDataString(name);
                byte[] data = GetBytes(url, out error);
                if (data == null)
                    return false;
                if (data.Length < 1024)
                {
                    error = name + " 下载内容不完整";
                    return false;
                }
                File.WriteAllBytes(Path.Combine(directory, name), data);
            }
            return true;
        }

        /// <summary>
        /// 生成等待编辑器退出后替换文件的批处理。返回脚本完整路径。
        /// 脚本先备份旧文件，替换失败会还原；替换成功后重启编辑器。
        /// </summary>
        public static string CreateHelper(string stageDirectory, string pluginDirectory,
            string editorPath, int processId)
        {
            string helperPath = stageDirectory.TrimEnd('\\') + ".cmd";
            string backup = Path.Combine(stageDirectory, "backup");
            StringBuilder text = new StringBuilder();
            text.AppendLine("@echo off");
            text.AppendLine("setlocal");
            text.AppendLine("set \"TARGET=" + pluginDirectory + "\"");
            text.AppendLine("set \"STAGE=" + stageDirectory + "\"");
            text.AppendLine("set \"BACK=" + backup + "\"");
            text.AppendLine("set \"EXE=" + editorPath + "\"");
            text.AppendLine("set \"PID=" + processId + "\"");
            text.AppendLine("set \"LOG=%TEMP%\\ClickCircle-update.log\"");
            text.AppendLine("for %%D in (\"%EXE%\") do set \"EXEDIR=%%~dpD\"");
            text.AppendLine("echo [%DATE% %TIME%] helper start pid=%PID% >> \"%LOG%\" 2>&1");
            text.AppendLine("set /a WAIT=0");
            text.AppendLine(":wait");
            text.AppendLine("tasklist /fi \"pid eq %PID%\" 2>nul | find \"%PID%\" >nul");
            text.AppendLine("if errorlevel 1 goto replace");
            text.AppendLine("set /a WAIT+=1");
            text.AppendLine("if %WAIT% GEQ 15 goto force");
            text.AppendLine("ping -n 2 127.0.0.1 >nul");
            text.AppendLine("goto wait");
            text.AppendLine(":force");
            text.AppendLine("echo [%DATE% %TIME%] force stop pid=%PID% >> \"%LOG%\" 2>&1");
            text.AppendLine("taskkill /pid %PID% /f >nul 2>&1");
            text.AppendLine("ping -n 3 127.0.0.1 >nul");
            text.AppendLine(":replace");
            text.AppendLine("if not exist \"%BACK%\" mkdir \"%BACK%\" >nul 2>&1");
            text.AppendLine("for %%F in (ClickCircle.pvp ClickCircle.Core.dll PDFXEdit.dll) do (");
            text.AppendLine("    if exist \"%TARGET%\\%%F\" copy /y \"%TARGET%\\%%F\" \"%BACK%\\%%F\" >nul 2>&1");
            text.AppendLine(")");
            text.AppendLine("for %%F in (ClickCircle.pvp ClickCircle.Core.dll PDFXEdit.dll) do (");
            text.AppendLine("    copy /y \"%STAGE%\\%%F\" \"%TARGET%\\%%F\" >nul 2>&1");
            text.AppendLine("    if errorlevel 1 goto rollback");
            text.AppendLine(")");
            text.AppendLine("echo [%DATE% %TIME%] replaced >> \"%LOG%\" 2>&1");
            text.AppendLine("if exist \"%EXE%\" start \"\" /d \"%EXEDIR%\" \"%EXE%\"");
            text.AppendLine("rmdir /s /q \"%STAGE%\" >nul 2>&1");
            text.AppendLine("del \"%~f0\" >nul 2>&1");
            text.AppendLine("exit /b 0");
            text.AppendLine(":rollback");
            text.AppendLine("echo [%DATE% %TIME%] rollback >> \"%LOG%\" 2>&1");
            text.AppendLine("for %%F in (ClickCircle.pvp ClickCircle.Core.dll PDFXEdit.dll) do (");
            text.AppendLine("    if exist \"%BACK%\\%%F\" copy /y \"%BACK%\\%%F\" \"%TARGET%\\%%F\" >nul 2>&1");
            text.AppendLine(")");
            text.AppendLine("if exist \"%EXE%\" start \"\" /d \"%EXEDIR%\" \"%EXE%\"");
            text.AppendLine("rmdir /s /q \"%STAGE%\" >nul 2>&1");
            text.AppendLine("del \"%~f0\" >nul 2>&1");
            text.AppendLine("exit /b 1");
            // 批处理里含编辑器安装路径，必须用系统代码页（中文 Windows 为 GBK）；
            // 用 ASCII 会把“桌面”这类目录写成问号，导致替换和重启全部失败。
            File.WriteAllText(helperPath, text.ToString(), Encoding.Default);
            return helperPath;
        }

        /// <summary>以隐藏窗口启动更新批处理。</summary>
        public static bool StartHelper(string helperPath, out string error)
        {
            error = null;
            try
            {
                ProcessStartInfo info = new ProcessStartInfo("cmd.exe", "/c \"" + helperPath + "\"");
                info.UseShellExecute = false;
                info.CreateNoWindow = true;
                info.WindowStyle = ProcessWindowStyle.Hidden;
                Process.Start(info);
                return true;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return false;
            }
        }

        private static string GetText(string url, out string error)
        {
            byte[] data = GetBytes(url, out error);
            return data == null ? null : Encoding.UTF8.GetString(data);
        }

        private static byte[] GetBytes(string url, out string error)
        {
            error = null;
            try
            {
                try { ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12; }
                catch (NotSupportedException) { }
                HttpWebRequest request = (HttpWebRequest)WebRequest.Create(url);
                request.UserAgent = "ClickCircle/" + Plugin.VersionText;
                request.Timeout = 15000;
                request.ReadWriteTimeout = 30000;
                request.AutomaticDecompression = DecompressionMethods.GZip;
                using (HttpWebResponse response = (HttpWebResponse)request.GetResponse())
                using (Stream stream = response.GetResponseStream())
                {
                    if (stream == null)
                    {
                        error = "服务器返回空内容";
                        return null;
                    }
                    using (MemoryStream buffer = new MemoryStream())
                    {
                        stream.CopyTo(buffer);
                        return buffer.ToArray();
                    }
                }
            }
            catch (WebException exception)
            {
                HttpWebResponse response = exception.Response as HttpWebResponse;
                if (response == null)
                    error = exception.Message;
                else if (response.StatusCode == HttpStatusCode.Forbidden &&
                    response.Headers["X-RateLimit-Remaining"] == "0")
                    error = "GitHub 访问次数已用完，请稍后再试";
                else
                    error = "HTTP " + (int)response.StatusCode + " " + response.StatusDescription;
                return null;
            }
            catch (Exception exception)
            {
                error = exception.Message;
                return null;
            }
        }
    }
}
