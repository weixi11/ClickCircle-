using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using ClickCircle;

// 面板可缩放的回归：边框可拖、客户区有下限、大小能存进设置并在重开后恢复。
internal static class PanelSizeTests
{
    private static int checkedCases;

    private static void Expect(bool condition, string description)
    {
        if (!condition)
            throw new Exception("Failed: " + description);
        ++checkedCases;
    }

    private static void CheckDefaults()
    {
        Expect(StampPanel.MinimumPanelWidth < StampPanel.DefaultPanelWidth,
            "最小宽度小于默认宽度");
        Expect(StampPanel.MinimumPanelHeight < StampPanel.DefaultPanelHeight,
            "最小高度小于默认高度");
        using (StampPanel panel = new StampPanel())
        {
            Expect(panel.FormBorderStyle == FormBorderStyle.SizableToolWindow,
                "面板边框可拖动缩放且保持工具窗口样式");
            Expect(panel.MaximizeBox == false && panel.MinimizeBox == false,
                "不显示最大化与最小化按钮");
            Expect(panel.ShowInTaskbar == false, "不进入任务栏");
            Expect(panel.ClientSize.Width == StampPanel.DefaultPanelWidth &&
                panel.ClientSize.Height == StampPanel.DefaultPanelHeight,
                "默认客户区为 480 x 566，实际 " + panel.ClientSize);
            Expect(panel.MinimumSize.Width >= StampPanel.MinimumPanelWidth &&
                panel.MinimumSize.Height >= StampPanel.MinimumPanelHeight,
                "窗口最小尺寸不小于客户区下限，实际 " + panel.MinimumSize);
        }
    }

    private static void CheckApplySettings()
    {
        using (StampPanel panel = new StampPanel())
        {
            panel.ApplySettings(new PluginSettings { PanelWidth = 900, PanelHeight = 800 });
            Expect(panel.ClientSize.Width == 900 && panel.ClientSize.Height == 800,
                "按设置恢复客户区大小，实际 " + panel.ClientSize);

            panel.ApplySettings(new PluginSettings { PanelWidth = 100, PanelHeight = 100 });
            Expect(panel.ClientSize.Width >= StampPanel.MinimumPanelWidth &&
                panel.ClientSize.Height >= StampPanel.MinimumPanelHeight,
                "过小的设置被撑到客户区下限，实际 " + panel.ClientSize);

            panel.ClientSize = new Size(720, 640);
            Expect(panel.GetSettings().PanelWidth == 720 &&
                panel.GetSettings().PanelHeight == 640,
                "GetSettings 反映当前窗口大小");
        }
    }

    private static void CheckSettingsRoundTrip()
    {
        string directory = Path.Combine(Path.GetTempPath(), "ClickCirclePanelSize-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        string path = Path.Combine(directory, "settings.xml");
        try
        {
            PluginSettings saved = new PluginSettings { PanelWidth = 720, PanelHeight = 640, Separator = "-" };
            Expect(saved.Save(path), "保存含窗口大小的设置");
            PluginSettings restored = PluginSettings.Load(path);
            Expect(restored.PanelWidth == 720 && restored.PanelHeight == 640,
                "往返保留窗口大小，实际 " + restored.PanelWidth + " x " + restored.PanelHeight);
            Expect(restored.Separator == "-", "往返不影响其它设置");

            File.WriteAllText(path, "<CircleStampSettings><WidthMm>2</WidthMm>" +
                "<HeightMm>3</HeightMm><PickHotkey>F9</PickHotkey><Separator>/</Separator>" +
                "</CircleStampSettings>");
            PluginSettings legacy = PluginSettings.Load(path);
            Expect(legacy.PanelWidth == StampPanel.DefaultPanelWidth &&
                legacy.PanelHeight == StampPanel.DefaultPanelHeight,
                "旧设置没有窗口大小则用默认值");

            File.WriteAllText(path, "<CircleStampSettings><PanelWidth>99999</PanelWidth>" +
                "<PanelHeight>1</PanelHeight></CircleStampSettings>");
            PluginSettings clamped = PluginSettings.Load(path);
            Expect(clamped.PanelWidth == 4000, "过大的宽度被夹到 4000，实际 " + clamped.PanelWidth);
            Expect(clamped.PanelHeight == StampPanel.MinimumPanelHeight,
                "过小的高度被夹到下限，实际 " + clamped.PanelHeight);

            File.WriteAllText(path, "<CircleStampSettings><PanelWidth>0</PanelWidth>" +
                "<PanelHeight>-40</PanelHeight></CircleStampSettings>");
            PluginSettings invalid = PluginSettings.Load(path);
            Expect(invalid.PanelWidth == StampPanel.DefaultPanelWidth &&
                invalid.PanelHeight == StampPanel.DefaultPanelHeight,
                "非正数回落默认值");
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
            Directory.Delete(directory);
        }
    }

    [STAThread]
    public static int Main()
    {
        try
        {
            CheckDefaults();
            CheckApplySettings();
            CheckSettingsRoundTrip();
            Console.WriteLine("PanelSize: " + checkedCases + " cases passed.");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(exception);
            return 1;
        }
    }
}
