using System;
using System.IO;
using ClickCircle;

// 在线更新：版本比较、标签解析、更新批处理生成（不访问网络）。
internal static class UpdateTests
{
    private static int checkedCases;

    private static void Expect(bool condition, string description)
    {
        if (!condition)
            throw new Exception("Failed: " + description);
        ++checkedCases;
    }

    public static int Main()
    {
        try
        {
            int major, minor, patch;
            Expect(UpdateService.TryParseVersion("v1.6.5", out major, out minor, out patch) &&
                major == 1 && minor == 6 && patch == 5, "解析 v1.6.5");
            Expect(UpdateService.TryParseVersion("1.10.0", out major, out minor, out patch) &&
                minor == 10, "解析 1.10.0");
            Expect(!UpdateService.TryParseVersion("main", out major, out minor, out patch),
                "拒绝非版本标签");
            Expect(!UpdateService.TryParseVersion("", out major, out minor, out patch),
                "拒绝空标签");

            Expect(UpdateService.CompareVersions("v1.6.6", "1.6.5") > 0, "新版本大于旧版本");
            Expect(UpdateService.CompareVersions("1.6.5", "v1.6.5") == 0, "忽略 v 前缀");
            Expect(UpdateService.CompareVersions("1.10.0", "1.9.9") > 0, "按数字而不是字符串比较");
            Expect(UpdateService.CompareVersions("1.6.5", "1.6.6") < 0, "旧版本小于新版本");
            Expect(UpdateService.CompareVersions("bad", "bad") == 0, "两个无效版本视为相等");

            const string json =
                "[{\"name\":\"v1.6.4\"},{\"name\":\"v1.6.5\"},{\"name\":\"main\"},{\"name\":\"v1.10.0\"}]";
            Expect(UpdateService.FindLatestTag(json) == "v1.10.0", "挑出版本最大的标签");
            Expect(UpdateService.FindLatestTag("[]") == null, "空标签列表");
            Expect(UpdateService.FindLatestTag(null) == null, "空 JSON");
            Expect(UpdateService.FindLatestTag("[{\"name\":\"main\"}]") == null, "只有非版本标签");

            Expect(UpdateService.ParseVersionFile("v1.6.6\n") == "v1.6.6", "解析 version.txt");
            Expect(UpdateService.ParseVersionFile("\uFEFFv1.6.6") == "v1.6.6", "忽略 BOM");
            Expect(UpdateService.ParseVersionFile("# 注释\n\n1.6.6\n") == "1.6.6", "忽略注释与空行");
            Expect(UpdateService.ParseVersionFile("not-a-version") == null, "拒绝无效版本文件");
            Expect(UpdateService.ParseVersionFile("") == null, "空版本文件");

            string stage = Path.Combine(Path.GetTempPath(),
                "ClickCircle-UpdateTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(stage);
            string helper = null;
            try
            {
                helper = UpdateService.CreateHelper(stage, @"C:\PluginDir",
                    @"C:\Editor\PDFXEdit.exe", 1234);
                Expect(File.Exists(helper), "生成更新批处理");
                string text = File.ReadAllText(helper);
                Expect(text.Contains("set \"PID=1234\""), "批处理等待原进程退出");
                Expect(text.Contains(@"C:\PluginDir"), "批处理目标目录");
                Expect(text.Contains(@"C:\Editor\PDFXEdit.exe"), "批处理重启编辑器");
                Expect(text.Contains("goto rollback"), "替换失败会回滚");
                Expect(text.Contains("taskkill /pid %PID% /f"), "进程不退时强制结束");
                Expect(text.Contains("ClickCircle-update.log"), "更新过程写入日志");
                Expect(text.Contains("start \"\" /d \"%EXEDIR%\""), "重启时使用编辑器目录");
                Expect(!text.Contains("%~dp0") || text.Contains("del \"%~f0\""), "结束时删除自身");
                Expect(new FileInfo(helper).Length < 4096, "批处理体积很小");
            }
            finally
            {
                if (helper != null && File.Exists(helper)) File.Delete(helper);
                if (Directory.Exists(stage)) Directory.Delete(stage, true);
            }

            Console.WriteLine("Update: " + checkedCases + " cases passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
