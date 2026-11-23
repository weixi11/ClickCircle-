using System;
using System.Drawing;

namespace ClickCircle
{
    /// <summary>
    /// 新标注自动套用的样式：主题/作者/标签等字段，颜色、边框、不透明度，
    /// 以及测量标注的比例尺与数字格式。实例同时用作 settings.xml 的序列化载体。
    /// </summary>
    public sealed class AnnotStyle
    {
        public AnnotStyle()
        {
            Enabled = false;
            MeasureOnly = true;
            Subject = String.Empty;
            Author = String.Empty;
            Contents = String.Empty;
            StrokeArgb = ToArgb(255, 0, 0);
            FillArgb = ToArgb(255, 255, 255);
            UseFill = false;
            BorderWidthPt = 4.0;
            OpacityPercent = 60;
            Dashed = false;
            MetersPerMm = 0;
            DecimalPlaces = -1;
            UseThousands = true;
        }

        /// <summary>是否对新标注自动套用本样式。</summary>
        public bool Enabled { get; set; }

        /// <summary>只套用到测量标注（周长/面积等），关闭时套用到所有新标注。</summary>
        public bool MeasureOnly { get; set; }

        /// <summary>主题；留空表示不改。</summary>
        public string Subject { get; set; }

        /// <summary>作者；留空表示不改。</summary>
        public string Author { get; set; }

        /// <summary>标签 / 正文；留空表示不改。</summary>
        public string Contents { get; set; }

        /// <summary>线条颜色，0xAARRGGBB。</summary>
        public int StrokeArgb { get; set; }

        /// <summary>填充颜色，0xAARRGGBB；仅 UseFill 为真时使用。</summary>
        public int FillArgb { get; set; }

        public bool UseFill { get; set; }

        /// <summary>边框宽度（点）。</summary>
        public double BorderWidthPt { get; set; }

        /// <summary>不透明度百分比 0–100。</summary>
        public int OpacityPercent { get; set; }

        /// <summary>边框是否使用虚线。</summary>
        public bool Dashed { get; set; }

        /// <summary>比例尺：图纸 1 毫米等于多少米；0 表示保持标注原有比例尺。</summary>
        public double MetersPerMm { get; set; }

        /// <summary>测量数字的小数位；-1 表示保持原设置。</summary>
        public int DecimalPlaces { get; set; }

        /// <summary>测量数字是否使用千位分隔符。</summary>
        public bool UseThousands { get; set; }

        public static AnnotStyle Default()
        {
            return new AnnotStyle();
        }

        public AnnotStyle Clone()
        {
            return new AnnotStyle {
                Enabled = Enabled,
                MeasureOnly = MeasureOnly,
                Subject = Subject,
                Author = Author,
                Contents = Contents,
                StrokeArgb = StrokeArgb,
                FillArgb = FillArgb,
                UseFill = UseFill,
                BorderWidthPt = BorderWidthPt,
                OpacityPercent = OpacityPercent,
                Dashed = Dashed,
                MetersPerMm = MetersPerMm,
                DecimalPlaces = DecimalPlaces,
                UseThousands = UseThousands
            };
        }

        /// <summary>把越界或无效的数值夹回可用范围；字段保留原始文本。</summary>
        public void Normalize()
        {
            Subject = Subject ?? String.Empty;
            Author = Author ?? String.Empty;
            Contents = Contents ?? String.Empty;
            if (!IsFinite(BorderWidthPt) || BorderWidthPt <= 0)
                BorderWidthPt = 4.0;
            BorderWidthPt = Math.Min(20.0, Math.Max(0.1, Math.Round(BorderWidthPt, 2)));
            OpacityPercent = Math.Min(100, Math.Max(0, OpacityPercent));
            if (!IsFinite(MetersPerMm) || MetersPerMm < 0)
                MetersPerMm = 0;
            if (MetersPerMm > 10000)
                MetersPerMm = 10000;
            DecimalPlaces = Math.Min(6, Math.Max(-1, DecimalPlaces));
            StrokeArgb = StrokeArgb & unchecked((int)0x00FFFFFF);
            FillArgb = FillArgb & unchecked((int)0x00FFFFFF);
        }

        public Color StrokeColor { get { return ToColor(StrokeArgb); } }
        public Color FillColor { get { return ToColor(FillArgb); } }

        public static int ToArgb(int red, int green, int blue)
        {
            return (0xFF << 24) | ((red & 0xFF) << 16) | ((green & 0xFF) << 8) | (blue & 0xFF);
        }

        /// <summary>0xAARRGGBB（忽略高位透明度）转成不透明 Color。</summary>
        public static Color ToColor(int argb)
        {
            return Color.FromArgb(255, (argb >> 16) & 0xFF, (argb >> 8) & 0xFF, argb & 0xFF);
        }

        private static bool IsFinite(double value)
        {
            return !Double.IsNaN(value) && !Double.IsInfinity(value);
        }

        /// <summary>把毫米比例尺换成每个 PDF 点对应的米数。</summary>
        public static double MetersPerPoint(double metersPerMillimeter)
        {
            return metersPerMillimeter * 25.4 / 72.0;
        }

        /// <summary>“1 mm = 0.5 m”形式的比例尺文本；0 返回空串。</summary>
        public static string FormatRatio(double metersPerMillimeter)
        {
            if (!(metersPerMillimeter > 0))
                return String.Empty;
            return "1 mm = " + metersPerMillimeter.ToString("0.###",
                System.Globalization.CultureInfo.InvariantCulture) + " m";
        }
    }
}
