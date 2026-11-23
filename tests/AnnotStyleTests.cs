using System;
using System.Drawing;
using System.IO;
using ClickCircle;

// 标注样式：默认值、数值夹取、颜色与比例尺换算、settings.xml 往返（不依赖编辑器）。
internal static class AnnotStyleTests
{
    private static int checkedCases;

    private static void Expect(bool condition, string description)
    {
        if (!condition)
            throw new Exception("Failed: " + description);
        ++checkedCases;
    }

    private static bool SameRgb(Color color, int red, int green, int blue)
    {
        return color.R == red && color.G == green && color.B == blue && color.A == 255;
    }

    public static int Main()
    {
        try
        {
            AnnotStyle defaults = AnnotStyle.Default();
            Expect(!defaults.Enabled, "默认不启用");
            Expect(defaults.MeasureOnly, "默认只套测量标注");
            Expect(defaults.BorderWidthPt == 4.0, "默认边框 4pt");
            Expect(defaults.OpacityPercent == 60, "默认不透明度 60");
            Expect(defaults.MetersPerMm == 0, "默认不改比例尺");
            Expect(defaults.DecimalPlaces == -1, "默认不改小数位");
            Expect(defaults.UseThousands, "默认千位分隔");
            Expect(defaults.Subject == String.Empty && defaults.Author == String.Empty &&
                defaults.Contents == String.Empty, "默认字段为空");

            AnnotStyle bad = new AnnotStyle {
                BorderWidthPt = 0, OpacityPercent = 150, MetersPerMm = Double.NaN,
                DecimalPlaces = 9, Subject = null, Author = null, Contents = null
            };
            bad.Normalize();
            Expect(bad.BorderWidthPt == 4.0, "边框宽度 0 回落 4");
            Expect(bad.OpacityPercent == 100, "不透明度夹到 100");
            Expect(bad.MetersPerMm == 0, "NaN 比例尺归零");
            Expect(bad.DecimalPlaces == 6, "小数位夹到 6");
            Expect(bad.Subject == String.Empty && bad.Author == String.Empty &&
                bad.Contents == String.Empty, "null 字段变空串");

            AnnotStyle clamp = new AnnotStyle { BorderWidthPt = -3, OpacityPercent = -1 };
            clamp.Normalize();
            Expect(clamp.BorderWidthPt == 4.0, "负边框宽度回落 4");
            Expect(clamp.OpacityPercent == 0, "不透明度夹到 0");

            AnnotStyle huge = new AnnotStyle { BorderWidthPt = 99, MetersPerMm = 1e9, DecimalPlaces = -9 };
            huge.Normalize();
            Expect(huge.BorderWidthPt == 20.0, "边框宽度夹到 20");
            Expect(huge.MetersPerMm == 10000, "比例尺夹到 10000");
            Expect(huge.DecimalPlaces == -1, "小数位下限 -1");

            Expect(AnnotStyle.ToArgb(255, 0, 0) == unchecked((int)0xFFFF0000), "红色 ARGB");
            Expect(SameRgb(AnnotStyle.Default().StrokeColor, 255, 0, 0), "默认线条颜色为红");
            Expect(SameRgb(AnnotStyle.Default().FillColor, 255, 255, 255), "默认填充颜色为白");

            Expect(AnnotStyle.FormatRatio(0.5) == "1 mm = 0.5 m", "比例尺文本 0.5");
            Expect(AnnotStyle.FormatRatio(0) == String.Empty, "比例尺 0 无文本");
            Expect(Math.Abs(AnnotStyle.MetersPerPoint(1) - 25.4 / 72.0) < 1e-9, "毫米转角点换算");
            Expect(Math.Abs(AnnotStyle.MetersPerPoint(0.5) - 0.5 * 25.4 / 72.0) < 1e-9, "0.5mm 换算");

            AnnotStyle original = new AnnotStyle { Enabled = true, Subject = "周长" };
            AnnotStyle copy = original.Clone();
            copy.Subject = "面积";
            Expect(original.Subject == "周长", "克隆互不影响");
            Expect(copy.Enabled, "克隆保留开关");

            string directory = Path.Combine(Path.GetTempPath(),
                "ClickCircle-StyleTest-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            string settingsPath = Path.Combine(directory, "settings.xml");
            try
            {
                PluginSettings settings = new PluginSettings();
                settings.Style = new AnnotStyle {
                    Enabled = true, MeasureOnly = false, Subject = "周长", Author = "张三",
                    Contents = "FOR R.G. T2", StrokeArgb = AnnotStyle.ToArgb(0, 0, 255),
                    FillArgb = AnnotStyle.ToArgb(255, 255, 0), UseFill = true,
                    BorderWidthPt = 4, OpacityPercent = 60, Dashed = true,
                    MetersPerMm = 0.5, DecimalPlaces = 2, UseThousands = true
                };
                Expect(settings.Save(settingsPath), "保存设置");
                PluginSettings loaded = PluginSettings.Load(settingsPath);
                Expect(loaded.Style != null, "样式被反序列化");
                Expect(loaded.Style.Enabled && !loaded.Style.MeasureOnly, "开关往返");
                Expect(loaded.Style.Subject == "周长" && loaded.Style.Author == "张三" &&
                    loaded.Style.Contents == "FOR R.G. T2", "字段往返");
                Expect(loaded.Style.UseFill && loaded.Style.Dashed, "填充与虚线往返");
                Expect(loaded.Style.MetersPerMm == 0.5 && loaded.Style.DecimalPlaces == 2,
                    "测量参数往返");
                Expect(SameRgb(loaded.Style.StrokeColor, 0, 0, 255), "线条颜色往返");

                PluginSettings dirty = new PluginSettings();
                dirty.Style = new AnnotStyle { BorderWidthPt = 0, OpacityPercent = 500, MetersPerMm = -1 };
                Expect(dirty.Save(settingsPath), "保存越界样式");
                PluginSettings fixedUp = PluginSettings.Load(settingsPath);
                Expect(fixedUp.Style.BorderWidthPt == 4.0 && fixedUp.Style.OpacityPercent == 100 &&
                    fixedUp.Style.MetersPerMm == 0, "保存时夹取越界值");

                File.WriteAllText(settingsPath,
                    "<?xml version=\"1.0\" encoding=\"utf-8\"?><CircleStampSettings>" +
                    "<WidthMm>1.5</WidthMm><HeightMm>1.2</HeightMm><Separator>/</Separator>" +
                    "</CircleStampSettings>");
                PluginSettings legacy = PluginSettings.Load(settingsPath);
                Expect(legacy.Style != null && !legacy.Style.Enabled, "旧版设置补默认样式");

                Expect(PluginSettings.NormalizeStyle(null) != null, "null 样式变默认");
                Expect(PluginSettings.NormalizeStyle(null).BorderWidthPt == 4.0, "null 样式已夹取");
            }
            finally
            {
                try { Directory.Delete(directory, true); }
                catch (Exception) { }
            }
        }
        catch (Exception exception)
        {
            Console.WriteLine("AnnotStyleTests FAILED: " + exception.Message);
            return 1;
        }
        Console.WriteLine("AnnotStyleTests: " + checkedCases + " cases passed.");
        return 0;
    }
}
