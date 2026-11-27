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
            Expect(panel.Text == "取字连续圈注", "默认标题不带版本号");
            panel.SetVersion("9.8.7");
            Expect(panel.Text == "取字连续圈注 v9.8.7", "标题显示版本号，实际 " + panel.Text);
            panel.SetVersion("  ");
            Expect(panel.Text == "取字连续圈注 v9.8.7", "空版本号保持原标题");
        }
        Expect(ClickCircle.Plugin.VersionText.Split('.').Length == 3,
            "插件版本文本为三段点分十进制，实际 " + ClickCircle.Plugin.VersionText);
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

    private static void CheckLayout()
    {
        using (StampPanel panel = new StampPanel())
        {
            TextBox subject = (TextBox)panel.Controls.Find("Subject", true)[0];
            ComboBox separator = (ComboBox)panel.Controls.Find("Separator", true)[0];
            DataGridView grid = (DataGridView)panel.Controls.Find("SubjectHistory", true)[0];

            NumericUpDown[] numbers = new NumericUpDown[2];
            int found = 0;
            Collect<NumericUpDown>(panel, ref numbers, ref found);
            Expect(found == 2, "两个尺寸输入框都在，实际 " + found);

            NumericUpDown width = numbers[0];
            NumericUpDown height = numbers[1];
            Expect(width.Width == height.Width, "宽高输入框等宽，实际 " +
                width.Width + " / " + height.Width);
            Expect(width.Width <= 140, "尺寸输入框不再拉满整行，实际 " + width.Width);
            Expect(subject.Width > width.Width * 2, "主题输入框明显宽于尺寸框，实际 " +
                subject.Width + " vs " + width.Width);
            Expect(separator.Width >= 100 && separator.Width <= 220,
                "分隔符下拉框宽度收敛，实际 " + separator.Width);
            Expect(panel.Controls.Count > 0 && panel.Width >= StampPanel.MinimumPanelWidth,
                "面板整体不小于下限");

            panel.CreateControl();
            panel.Show();
            Application.DoEvents();
            Expect(grid.Height >= 160, "历史表格在默认尺寸下有可用高度，实际 " + grid.Height);
            Expect(grid.RowTemplate.Height >= panel.Font.Height + 10,
                "表格行高跟随字号，实际 " + grid.RowTemplate.Height +
                " 字体高 " + panel.Font.Height);
            panel.Hide();
        }
    }

    private static void CheckPickHintPlacement()
    {
        Size hint = new Size(130, 24);
        Point placed = PickHintPlacement.NearCursor(new Point(960, 540),
            hint, new Rectangle(0, 0, 1920, 1040));
        Expect(placed.X == 960 + PickHintPlacement.OffsetX &&
            placed.Y == 540 + PickHintPlacement.OffsetY,
            "取字提示显示在鼠标右下，实际 " + placed);

        Point flipped = PickHintPlacement.NearCursor(new Point(1850, 1030),
            hint, new Rectangle(0, 0, 1920, 1040));
        Expect(flipped.X == 1850 - 130 - PickHintPlacement.OffsetX &&
            flipped.Y == 1030 - 24 - PickHintPlacement.OffsetY,
            "靠近右/下边缘时翻到鼠标左上，实际 " + flipped);

        Point clamped = PickHintPlacement.NearCursor(new Point(-50, -60),
            hint, new Rectangle(0, 0, 1920, 1040));
        Expect(clamped.X == 0 && clamped.Y == 0,
            "鼠标贴左上边缘时夹到工作区内，实际 " + clamped);

        Point offset = PickHintPlacement.NearCursor(new Point(300, 200),
            hint, new Rectangle(100, 50, 800, 600));
        Expect(offset.X == 300 + PickHintPlacement.OffsetX &&
            offset.Y == 200 + PickHintPlacement.OffsetY,
            "副屏工作区按自身坐标落点，实际 " + offset);
    }

    private static void Collect<T>(Control root, ref T[] slots, ref int count) where T : Control
    {
        foreach (Control child in root.Controls)
        {
            T match = child as T;
            if (match != null && count < slots.Length)
                slots[count++] = match;
            Collect<T>(child, ref slots, ref count);
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
            CheckLayout();
            CheckPickHintPlacement();
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
